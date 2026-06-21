using System.Collections.Concurrent;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Platforms.Windows.Bluetooth;
using Sefirah.Platforms.Windows.Utilities;
using Windows.ApplicationModel.Calls;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace Sefirah.Platforms.Windows.Calling;

public sealed class WindowsPhoneLineService(
    ILogger logger,
    BluetoothRadioManager bluetoothRadioManager,
    IDeviceManager deviceManager,
    DeviceRepository deviceRepository) : IPhoneLineService
{
    private const string ContainerIdKey = "System.Devices.ContainerId";
    private const string BluetoothDeviceAddressKey = "System.DeviceInterface.Bluetooth.DeviceAddress";

    private readonly ConcurrentDictionary<string, DeviceInformation> pltDeviceById = new();
    private readonly ConcurrentDictionary<Guid, PhoneLine> phoneLinesByLineId = new();

    private DeviceWatcher? pltDeviceWatcher;
    private PhoneLineWatcher? phoneLineWatcher;
    private CancellationTokenSource? refreshCts;

    private PairedDevice? ActiveDevice => deviceManager.ActiveDevice;
    private string? ActiveTransportId => ActiveDevice?.CallsTransportDeviceId;

    public CallingLineStatus LineStatus { get; private set; } = CallingLineStatus.NotLinked;

    public event EventHandler<CallingLineStatus>? LineStatusChanged;
    public event EventHandler<IPhoneCall>? CallStateChanged;

    public async Task Initialize()
    {
        var contractOk = CallingFeatureUtils.IsBluetoothCallingSupportedByPlatform();
        if (!contractOk)
            logger.Warn("CallsPhoneContract v5 not present on this OS.");

        var transportApisOk = CallingFeatureUtils.TryUnlockPhoneLineTransportDeviceAPIs(logger);
        if (!transportApisOk)
            logger.Warn("Phone line transport LAF not unlocked.");

        var callingSupported = contractOk && transportApisOk;
        if (!callingSupported) 
        {
            SetLineStatus(CallingLineStatus.NotSupported);
            return;
        }

        if (!await bluetoothRadioManager.RefreshAsync()) 
        {
            SetLineStatus(CallingLineStatus.BluetoothAdapterNotFound);
            return;
        }

        deviceManager.ActiveDeviceChanged += OnActiveDeviceChanged;
        bluetoothRadioManager.RadioStateChanged += OnBluetoothRadioStateChanged;

        try
        {
            pltDeviceWatcher = DeviceInformation.CreateWatcher(PhoneLineTransportDevice.GetDeviceSelector(), [BluetoothDeviceAddressKey]);
            pltDeviceWatcher.Added += OnPltDeviceAdded;
            pltDeviceWatcher.Removed += OnPltDeviceRemoved;
            pltDeviceWatcher.Updated += OnPltDeviceUpdated;
            pltDeviceWatcher.Start();
            logger.Debug("Phone line transport watcher started");
        }
        catch (Exception ex)
        {
            logger.Error("Error creating phone line transport watcher", ex);
        }

        await StartPhoneLineWatcherAsync();

        PhoneCallManager.CallStateChanged += OnCallStateChanged;

        _ = RefreshStateAsync();
    }

    private async Task StartPhoneLineWatcherAsync()
    {
        try
        {
            var store = await PhoneCallManager.RequestStoreAsync();
            phoneLineWatcher = store.RequestLineWatcher();
            phoneLineWatcher.LineAdded += OnPhoneLineAdded;
            phoneLineWatcher.LineRemoved += OnPhoneLineRemoved;
            phoneLineWatcher.Stopped += OnPhoneLineWatcherStopped;
            phoneLineWatcher.LineUpdated += OnPhoneLineUpdated;
            phoneLineWatcher.Start();
        }
        catch (Exception ex)
        {
            logger.Error("Failed to start PhoneLine watcher", ex);
            phoneLineWatcher = null;
        }
    }

    private async void OnPhoneLineUpdated(PhoneLineWatcher sender, PhoneLineWatcherEventArgs args)
    {
        var line = await PhoneLine.FromIdAsync(args.LineId);
        if (line is null) return;

        logger.Debug($"PhoneLine updated {args.LineId} (DisplayName={line.DisplayName})");
        phoneLinesByLineId[args.LineId] = line;
        await RefreshStateAsync();
        CheckActiveCalls(line);
    }

    private void OnPhoneLineWatcherStopped(PhoneLineWatcher sender, object args) => logger.Warn("PhoneLine watcher stopped");

    private async void OnPhoneLineAdded(PhoneLineWatcher sender, PhoneLineWatcherEventArgs args)
    {
        var line = await PhoneLine.FromIdAsync(args.LineId);
        if (line is null) return; 

        logger.Debug($"PhoneLine added/updated {args.LineId} (DisplayName={line.DisplayName})");
        phoneLinesByLineId[args.LineId] = line;
        await RefreshStateAsync();
        CheckActiveCalls(line);
    }

    private async void OnPhoneLineRemoved(PhoneLineWatcher sender, PhoneLineWatcherEventArgs args)
    {
        logger.Debug($"PhoneLine removed {args.LineId}");
        phoneLinesByLineId.TryRemove(args.LineId, out _);
        await RefreshStateAsync();
    }

    private void OnCallStateChanged(object? sender, object e) => CheckActiveCalls();

    private async void CheckActiveCalls(PhoneLine line)
    {
        try
        {
            var result = await line.GetAllActivePhoneCallsAsync();
            foreach (var activeCall in result.AllActivePhoneCalls)
            {
                CallStateChanged?.Invoke(this, new PhoneCall(activeCall));
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to read active calls for line {line.Id}", ex);
        }
    }

    private async void CheckActiveCalls()
    {
        var seenCallIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (lineId, line) in phoneLinesByLineId)
        {
            try
            {
                var result = await line.GetAllActivePhoneCallsAsync();
                foreach (var activeCall in result.AllActivePhoneCalls)
                {
                    if (seenCallIds.Add(activeCall.CallId))
                        CallStateChanged?.Invoke(this, new PhoneCall(activeCall));
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to read active calls for line {lineId}", ex);
            }
        }
    }

    public async Task DialAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ActiveTransportId))
            return;

        var line = FindLineByTransport(ActiveTransportId);
        if (line is null)
        {
            logger.Debug($"No dialable line for transport {ActiveTransportId}");
            return;
        }

        try
        {
            var result = await line.DialWithResultAsync(phoneNumber, phoneNumber);
            var phoneCall = result.DialedCall;

            if (phoneCall is null)
            {
                logger.Warn($"DialWithResultAsync succeeded but returned null call for {phoneNumber}");
                return;
            }

            await phoneCall.ChangeAudioDeviceAsync(PhoneCallAudioDevice.LocalDevice);
            CallStateChanged?.Invoke(this, new PhoneCall(phoneCall));
        }
        catch (Exception ex)
        {
            logger.Error("DialAsync failed", ex);
        }
    }

    private async void OnActiveDeviceChanged(object? sender, PairedDevice? device)
    {
        refreshCts?.Cancel();
        refreshCts?.Dispose();
        refreshCts = new CancellationTokenSource();
        var ct = refreshCts.Token;

        try
        {
            await RefreshStateAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    public async Task RefreshStateAsync(CancellationToken cancellationToken = default)
    { 
        if (bluetoothRadioManager.RadioState is not RadioState.On) 
        {
            SetLineStatus(CallingLineStatus.BluetoothAdapterOff);
            return;
        }

        var device = ActiveDevice;
        if (device is null)
        {
            return;
        }

        var transportId = device.CallsTransportDeviceId;

        if (string.IsNullOrWhiteSpace(transportId))
        {
            if (!TryFindTransport(device.BluetoothAddress, device.Name, out transportId))
            {
                SetLineStatus(CallingLineStatus.DeviceNotPaired);
                return;
            }
            await SaveTransportIdAsync(device, transportId);
        }

        SetLineStatus(await ConnectTransportAsync(transportId, cancellationToken));
    }

    private async Task<CallingLineStatus> ConnectTransportAsync(string transportDeviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transportDeviceId)) return CallingLineStatus.NotLinked;

        try
        {
            var pltDevice = PhoneLineTransportDevice.FromId(transportDeviceId);
            if (pltDevice is null)
            {
                logger.Debug($"phoneline was null for {transportDeviceId}");
                return CallingLineStatus.TransportMissing;
            }

            var access = await pltDevice.RequestAccessAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (access is not DeviceAccessStatus.Allowed)
            {
                logger.Debug($"Access not allowed for {transportDeviceId}: {access}");
                return CallingLineStatus.RegistrationAccessDenied;
            }

            if (!pltDevice.IsRegistered())
                pltDevice.RegisterApp();

            if (!pltDevice.IsRegistered())
            {
                logger.Warn($"Registration failed for {transportDeviceId} — another app may own this line.");
                return CallingLineStatus.RegistrationAccessDenied;
            }

            if (!await pltDevice.ConnectAsync())
            {
                logger.Warn($"ConnectAsync returned false for {transportDeviceId}");
                return CallingLineStatus.NotLinked;
            }

            var line = FindLineByTransport(transportDeviceId);
            if (line is null)
            {
                logger.Debug($"No dialable PhoneLine for transport {transportDeviceId}");
                return CallingLineStatus.NotLinked;
            }

            return CallingLineStatus.Ready;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Warn($"ConnectTransportAsync failed for {transportDeviceId}", ex);
            return CallingLineStatus.NotLinked;
        }
    }

    private void SetLineStatus(CallingLineStatus status)
    {
        LineStatus = status;
        LineStatusChanged?.Invoke(this, status);
    }

    private async void OnBluetoothRadioStateChanged(RadioState radioState)
    {   
        await RefreshStateAsync();
    }

    private async void OnPltDeviceAdded(DeviceWatcher sender, DeviceInformation args)
    {
        logger.Debug($"PLT watcher added transport {args.Id} (Name={args.Name})");
        pltDeviceById[args.Id] = args;
        await RefreshStateAsync();
    }

    private async void OnPltDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        if (pltDeviceById.TryGetValue(args.Id, out var deviceInfo))
        {
            deviceInfo.Update(args);
            logger.Debug($"PLT watcher updated transport {args.Id}");
            await RefreshStateAsync();
        }
    }

    private async void OnPltDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        logger.Debug($"PLT watcher removed transport {args.Id}");
        pltDeviceById.TryRemove(args.Id, out _);

        await RefreshStateAsync();
    }

    private async Task SaveTransportIdAsync(PairedDevice device, string? transportId)
    {
        device.CallsTransportDeviceId = transportId;

        var entity = await deviceRepository.GetPairedDevice(device.Id).ConfigureAwait(false);
        if (entity is null) return;
        entity.CallsTransportDeviceId = transportId;
        deviceRepository.AddOrUpdateRemoteDevice(entity);
    }

    private PhoneLine? FindLineByTransport(string transportDeviceId) =>
        phoneLinesByLineId.Values.Where(p => p.CanDial).FirstOrDefault(pl => string.Equals(pl.TransportDeviceId, transportDeviceId, StringComparison.OrdinalIgnoreCase));

    private bool TryFindTransport(string? btAddress, string deviceName, out string transportId)
    {
        if (TryFindTransportByBluetoothAddress(btAddress, out transportId))
        {
            return true;
        }

        if (TryFindTransportByName(deviceName, out transportId))
        {
            return true;
        }

        return false;
    }

    private bool TryFindTransportByBluetoothAddress(string? bluetoothAddress, out string transportId)
    {
        transportId = string.Empty;
        if (string.IsNullOrWhiteSpace(bluetoothAddress))
            return false;

        var matches = pltDeviceById.Values
            .Where(d => DoBluetoothCallingAddressesMatch(d, bluetoothAddress))
            .Select(d => d.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (matches.Length == 1)
        {
            transportId = matches[0];
            return true;
        }

        return false;
    }

    private bool TryFindTransportByName(string deviceName, out string transportId)
    {
        transportId = string.Empty;

        var byWatcher = pltDeviceById.Values
            .Where(d => IsNameMatch(deviceName, d.Name))
            .Select(d => d.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (byWatcher.Length == 1)
        {
            transportId = byWatcher[0];
            return true;
        }

        var byPhoneLine = phoneLinesByLineId.Values
            .Where(line => IsNameMatch(deviceName, line.DisplayName))
            .Select(line => line.TransportDeviceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (byPhoneLine.Length == 1)
        {
            transportId = byPhoneLine[0];
            return true;
        }

        return false;
    }

    // Phone-line transport watcher entries expose the Bluetooth MAC on the interface key, while AEP
    // address can be null in this watcher context. Use the interface address for transport matching.
    private static bool DoBluetoothCallingAddressesMatch(DeviceInformation incomingDeviceInformation, string bluetoothAddress)
    {
        var normalizedStoredAddress = bluetoothAddress.Replace(":", string.Empty);
        if (incomingDeviceInformation.Properties.TryGetValue(BluetoothDeviceAddressKey, out var value) && value is string incomingBluetoothAddress)
        {
            return string.Equals(normalizedStoredAddress, incomingBluetoothAddress, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }


    private static bool IsNameMatch(string target, string? candidate) =>
        !string.IsNullOrEmpty(candidate) &&
        string.Equals(target.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase);
}
