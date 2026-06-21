using System.Collections.Concurrent;
using Sefirah.Data.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace Sefirah.Platforms.Windows.Bluetooth;

public sealed partial class BluetoothPairingService : IBluetoothPairingService, IDisposable
{
    private const string AepDeviceAddressKey = "System.Devices.Aep.DeviceAddress";

    private const DevicePairingKinds BluetoothCustomPairingKinds = DevicePairingKinds.ConfirmOnly | DevicePairingKinds.DisplayPin | DevicePairingKinds.ConfirmPinMatch;

    private DeviceWatcher? pairedWatcher;
    private readonly ConcurrentDictionary<string, DeviceInformation> pairedDevicesById = new();

    private DeviceWatcher? unpairedWatcher;
    private readonly ConcurrentDictionary<string, DeviceInformation> unpairedDevices = new();

    private TaskCompletionSource<DeviceInformation?>? matchTcs;
    private PairedDevice? scanTargetPhone;

    private string? pendingDiscoveryBluetoothName;
    private TaskCompletionSource<bool>? pendingDiscoveryResultTcs;
    private DeviceInformation? lastDiscoveredDevice;

    private readonly ILogger logger;
    private readonly BluetoothRadioManager bluetoothRadioManager;

    public bool IsBluetoothSupported => bluetoothRadioManager.IsBluetoothSupported;
    public bool IsBluetoothRadioOn => bluetoothRadioManager.IsBluetoothRadioOn;

    private BluetoothPairingState state = new(BluetoothPairingStep.Connectivity, BluetoothPairingStatus.NotStarted);
    public BluetoothPairingState State
    {
        get => state;
        private set
        {
            if (state != value)
            {
                state = value;
                StateChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<BluetoothPairingState>? StateChanged;

    public BluetoothPairingService(ILogger logger, BluetoothRadioManager bluetoothRadioManager)
    {
        this.logger = logger;
        this.bluetoothRadioManager = bluetoothRadioManager;
        bluetoothRadioManager.RadioStateChanged += OnBluetoothRadioStateChanged;
    }

    public async Task<bool> TryEnableBluetoothAsync()
    {
        return await bluetoothRadioManager.TryEnableAsync();
    }

    private async void OnBluetoothRadioStateChanged(RadioState radioState)
    {
        if (radioState is RadioState.Off or RadioState.Disabled)
        {
            State = new(BluetoothPairingStep.Connectivity, BluetoothPairingStatus.BluetoothRadioOff);
            Stop();
        }
        else if (radioState is RadioState.On)
        {
            await RefreshAsync();
        }
    }

    // this doesn't do anything now but can be useful
    public Task RefreshAsync()
    {
        if (!IsBluetoothSupported)
        {
            return Task.CompletedTask;
        }

        if (pairedWatcher is not null)
        {
            pairedWatcher.Added -= OnPairedAdded;
            pairedWatcher.Updated -= OnPairedUpdated;
            pairedWatcher.Removed -= OnPairedRemoved;
        }

        pairedWatcher = DeviceInformation.CreateWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true), [AepDeviceAddressKey], DeviceInformationKind.AssociationEndpoint);
        pairedWatcher.Added += OnPairedAdded;
        pairedWatcher.Updated += OnPairedUpdated;
        pairedWatcher.Removed += OnPairedRemoved;
        pairedWatcher.Start();
        return Task.CompletedTask;
    }

    public void Stop()
    {
        pairedDevicesById.Clear();
        StopUnpairedWatcher();
        lastDiscoveredDevice = null;

        if (pairedWatcher is not null && pairedWatcher.Status is DeviceWatcherStatus.Started)
        {
            pairedWatcher.Stop();
        }
    }

    private void StartUnpairedWatcher()
    {
        if (unpairedWatcher is null)
        {
            unpairedDevices.Clear();
            unpairedWatcher = DeviceInformation.CreateWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(false), [AepDeviceAddressKey]);
            unpairedWatcher.Added += OnUnpairedAdded;
            unpairedWatcher.Updated += OnUnpairedUpdated;
            unpairedWatcher.Removed += OnUnpairedRemoved;
        }

        if (unpairedWatcher.Status is DeviceWatcherStatus.Created or DeviceWatcherStatus.Stopped or DeviceWatcherStatus.Aborted)
        {
            unpairedWatcher.Start();
        }
    }

    private void StopUnpairedWatcher()
    {
        if (unpairedWatcher is null) return;
        unpairedWatcher.Added -= OnUnpairedAdded;
        unpairedWatcher.Updated -= OnUnpairedUpdated;
        unpairedWatcher.Removed -= OnUnpairedRemoved;
        unpairedDevices.Clear();
        unpairedWatcher = null;
    }

    private void OnUnpairedAdded(DeviceWatcher sender, DeviceInformation args)
    {
        unpairedDevices[args.Id] = args;
        TrySignalMatchIfTargetPhone(args);
    }

    private void OnUnpairedUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        if (!unpairedDevices.TryGetValue(args.Id, out var existing)) return;
        existing.Update(args);
        TrySignalMatchIfTargetPhone(existing);
    }

    private void OnUnpairedRemoved(DeviceWatcher sender, DeviceInformationUpdate args) => unpairedDevices.TryRemove(args.Id, out _);

    private void TrySignalMatchIfTargetPhone(DeviceInformation candidate)
    {
        var phone = scanTargetPhone;
        var tcs = matchTcs;
        if (phone is null || tcs is null) return;

        if (string.IsNullOrWhiteSpace(candidate.Name)) return;
        var win = Normalize(candidate.Name);
        if (string.IsNullOrEmpty(win)) return;

        foreach (var label in MatchLabels(phone))
        {
            var n = Normalize(label);
            if (n is null) continue;
            if (string.Equals(n, win, StringComparison.OrdinalIgnoreCase) ||
                n.Contains(win, StringComparison.OrdinalIgnoreCase) ||
                win.Contains(n, StringComparison.OrdinalIgnoreCase))
            {
                tcs.TrySetResult(candidate);
            }
        }
    }

    private async Task<bool> WaitForUnpairedDeviceAsync(CancellationToken cancellationToken)
    {
        matchTcs = new TaskCompletionSource<DeviceInformation?>(TaskCreationOptions.RunContinuationsAsynchronously);
        StartUnpairedWatcher();

        var matchTask = matchTcs.Task;
        var completedTask = await Task.WhenAny(matchTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));

        if (completedTask != matchTask)
        {
            logger.Debug("WaitForUnpairedDeviceAsync timed out");
            State = new(BluetoothPairingStep.Discovery, BluetoothPairingStatus.DeviceNotFound);
            return false;
        }

        var matched = await matchTask;
        if (matched is null)
        {
            logger.Debug("No matched device found");
            State = new(BluetoothPairingStep.Discovery, BluetoothPairingStatus.DeviceNotFound);
            return false;
        }
        lastDiscoveredDevice = matched;
        return true;
    }

    public async Task<bool> DiscoverAsync(PairedDevice phone, CancellationToken cancellationToken = default)
    {
        if (!IsBluetoothSupported) return false;
        lastDiscoveredDevice = null;

        if (!IsBluetoothRadioOn && !await bluetoothRadioManager.TryEnableAsync())
        {
            logger.Debug("DiscoverAsync skipped: bluetooth radio unavailable.");
            State = new(BluetoothPairingStep.Connectivity, BluetoothPairingStatus.BluetoothRadioOff);
            return false;
        }


        try
        {
            State = new(BluetoothPairingStep.Discovery, BluetoothPairingStatus.InProgress);
            scanTargetPhone = phone;

            var granted = await RequestBluetoothDiscoveryAsync(phone, cancellationToken).ConfigureAwait(false);
            if (!granted)
            {
                State = new(BluetoothPairingStep.Discovery, BluetoothPairingStatus.PhoneDiscoveryRequestDenied);
                return false;
            }

            return await WaitForUnpairedDeviceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            State = new(BluetoothPairingStep.Discovery, BluetoothPairingStatus.Cancelled);
            return false;
        }
        finally
        {
            StopUnpairedWatcher();
            scanTargetPhone = null;
            matchTcs = null;
            pendingDiscoveryBluetoothName = null;
        }
    }

    public async Task<bool> PairAsync(PairedDevice phone, Func<string, string, Task<bool>> confirmAsync, CancellationToken cancellationToken = default)
    {
        var discoveredDevice = lastDiscoveredDevice;
        if (discoveredDevice is null)
            return false;

        if (!IsBluetoothSupported) return false;

        if (!IsBluetoothRadioOn && !await bluetoothRadioManager.TryEnableAsync())
        {
            logger.Debug("PairAsync skipped: bluetooth radio unavailable.");
            State = new(BluetoothPairingStep.Connectivity, BluetoothPairingStatus.BluetoothRadioOff);
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        State = new(BluetoothPairingStep.Pairing, BluetoothPairingStatus.InProgress);

        try
        {
            if (discoveredDevice.Pairing.IsPaired)
            {
                State = new(BluetoothPairingStep.Ready, BluetoothPairingStatus.Success);
                phone.BluetoothAddress = GetBluetoothAddress(discoveredDevice);
                phone.BluetoothClassicDeviceId = discoveredDevice.Id;
                lastDiscoveredDevice = null;
                return true;
            }

            var customPairing = discoveredDevice.Pairing.Custom;
            async void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
            {
                var deferral = args.GetDeferral();
                try
                {
                    var accepted = await confirmAsync(args.DeviceInformation.Name, args.Pin).ConfigureAwait(false);
                    if (accepted) args.Accept();
                }
                catch (Exception ex)
                {
                    logger.Error("Custom pairing handler failed.", ex);
                }
                finally
                {
                    deferral.Complete();
                }
            }

            customPairing.PairingRequested += OnPairingRequested;
            try
            {
                var result = await customPairing.PairAsync(BluetoothCustomPairingKinds);
                var ok = result.Status is DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired;
                var bluetoothStatus = ok ? BluetoothPairingStatus.Success : BluetoothPairingStatus.PairingFailed;
                var pairingStep = ok ? BluetoothPairingStep.Ready : BluetoothPairingStep.Pairing;
                State = new(pairingStep, bluetoothStatus);
                if (!ok) return false;

                phone.BluetoothAddress = GetBluetoothAddress(discoveredDevice);
                phone.BluetoothClassicDeviceId = discoveredDevice.Id;
                lastDiscoveredDevice = null;
                return true;
            }
            finally
            {
                customPairing.PairingRequested -= OnPairingRequested;
            }
        }
        catch (OperationCanceledException)
        {
            State = new(BluetoothPairingStep.Pairing, BluetoothPairingStatus.Cancelled);
            return false;
        }
        catch (Exception ex)
        {
            logger.Error($"PairAsync failed for deviceId {discoveredDevice.Id}", ex);
            State = new(BluetoothPairingStep.Pairing, BluetoothPairingStatus.PairingFailed);
            return false;
        }
    }

    public async Task<DeviceUnpairingResultStatus> UnpairAsync(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return DeviceUnpairingResultStatus.Failed;
        }

        try
        {
            var deviceInfo = await DeviceInformation.CreateFromIdAsync(deviceId);
            if (deviceInfo is null) return DeviceUnpairingResultStatus.Failed;
            var result = await deviceInfo.Pairing.UnpairAsync();
            return result.Status;
        }
        catch (Exception ex)
        {
            logger.Error($"UnpairAsync failed for deviceId {deviceId}", ex);
            return DeviceUnpairingResultStatus.Failed;
        }
    }

    private async Task<bool> RequestBluetoothDiscoveryAsync(PairedDevice phone, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingDiscoveryResultTcs?.TrySetResult(false);
        pendingDiscoveryResultTcs = tcs;

        phone.SendMessage(new BluetoothPairingRequest());
        logger.Info($"Sent BluetoothPairingRequest for device {phone.Id}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(12));
        using var reg = timeoutCts.Token.Register(() => tcs.TrySetResult(false));

        var granted = await tcs.Task.ConfigureAwait(false);
        if (ReferenceEquals(pendingDiscoveryResultTcs, tcs))
        {
            pendingDiscoveryResultTcs = null;
        }

        return granted;
    }

    public void HandleBluetoothPairingResult(PairedDevice device, BluetoothPairingResult result)
    {
        logger.Info($"Received BluetoothPairingResult, device name: {result.DeviceName}");

        if (scanTargetPhone?.Id == device.Id && pendingDiscoveryResultTcs is not null)
        {
            if (!string.IsNullOrWhiteSpace(result.DeviceName))
            {
                pendingDiscoveryBluetoothName = result.DeviceName;
            }

            pendingDiscoveryResultTcs.TrySetResult(result.Granted);
        }
    }

    private void OnPairedAdded(DeviceWatcher sender, DeviceInformation args)
    {
        pairedDevicesById.TryAdd(args.Id, args);
    }

    private void OnPairedUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        if (pairedDevicesById.TryGetValue(args.Id, out var existing))
        {
            existing.Update(args);
        }
    }

    private void OnPairedRemoved(DeviceWatcher sender, DeviceInformationUpdate args) =>
        pairedDevicesById.TryRemove(args.Id, out _);

    private IEnumerable<string> MatchLabels(PairedDevice phone)
    {
        if (!string.IsNullOrWhiteSpace(phone.Name)) yield return phone.Name;
        if (!string.IsNullOrWhiteSpace(phone.Model)) yield return phone.Model;
        if (!string.IsNullOrWhiteSpace(pendingDiscoveryBluetoothName)) yield return pendingDiscoveryBluetoothName!;
    }

    private static string? Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return new string(name.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
    }

    private static string? GetBluetoothAddress(DeviceInformation d)
    {
        string? addr = null;
        if (d.Properties.TryGetValue(AepDeviceAddressKey, out var o) && o is string s && !string.IsNullOrWhiteSpace(s))
        {
            addr = s.Trim();
        }

        return addr;
    }

    public void Dispose()
    {
        bluetoothRadioManager.RadioStateChanged -= OnBluetoothRadioStateChanged;
        Stop();

        if (pairedWatcher is not null)
        {
            pairedWatcher.Added -= OnPairedAdded;
            pairedWatcher.Updated -= OnPairedUpdated;
            pairedWatcher.Removed -= OnPairedRemoved;
            pairedWatcher = null;
        }
    }
}
