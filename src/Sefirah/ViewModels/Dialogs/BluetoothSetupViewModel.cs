using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;

namespace Sefirah.ViewModels.Dialogs;

public sealed partial class BluetoothSetupViewModel : BaseViewModel
{
    private readonly PairedDevice activeDevice;
    private readonly IBluetoothPairingService bluetoothPairingService = Ioc.Default.GetRequiredService<IBluetoothPairingService>();
    private readonly DeviceRepository deviceRepository = Ioc.Default.GetRequiredService<DeviceRepository>();

    private CancellationTokenSource? operationCts;
    private TaskCompletionSource<bool>? inlinePairingDecision;
    private readonly Action closeDialog;

    public BluetoothSetupViewModel(PairedDevice phone, Action closeAction) : base()
    {
        activeDevice = phone;
        closeDialog = closeAction;
        ShowIntroPanel = true;
        InlinePairingDeviceName = string.Empty;
        InlinePairingCode = "—";
        bluetoothPairingService.StateChanged += OnStateChanged;
    }

    #region Properties

    public bool ShowDialogFooter => !ShowInlinePairingPanel;
    public bool ShowNoDevicesPanel => ShowBluetoothOffMessage || ShowDeviceNotFoundMessage || ShowPairingFailedMessage;

    [ObservableProperty] public partial bool ShowIntroPanel { get; set; }
    [ObservableProperty] public partial bool ShowPhonePermissionRetry { get; set; }
    [ObservableProperty] public partial bool ShowWaitingPhonePanel { get; set; }
    [ObservableProperty] public partial bool ShowScanningPanel { get; set; }
    [ObservableProperty] public partial bool ShowBluetoothOffMessage { get; set; }
    [ObservableProperty] public partial bool ShowDeviceNotFoundMessage { get; set; }
    [ObservableProperty] public partial bool ShowPairingFailedMessage { get; set; }
    [ObservableProperty] public partial bool ShowPairingPanel { get; set; }
    [ObservableProperty] public partial bool ShowInlinePairingPanel { get; set; }
    [ObservableProperty] public partial string InlinePairingDeviceName { get; set; }
    [ObservableProperty] public partial string InlinePairingCode { get; set; }

    partial void OnShowInlinePairingPanelChanged(bool value) => OnPropertyChanged(nameof(ShowDialogFooter));
    partial void OnShowBluetoothOffMessageChanged(bool value) => OnPropertyChanged(nameof(ShowNoDevicesPanel));
    partial void OnShowDeviceNotFoundMessageChanged(bool value) => OnPropertyChanged(nameof(ShowNoDevicesPanel));
    partial void OnShowPairingFailedMessageChanged(bool value) => OnPropertyChanged(nameof(ShowNoDevicesPanel));

    #endregion

    private async void OnStateChanged(object? sender, BluetoothPairingState s)
    {
        await dispatcher.EnqueueAsync(() => ApplyState(s));
    }

    [RelayCommand]
    private void Cancel()
    {
        inlinePairingDecision?.TrySetResult(false);
        operationCts?.Cancel();
        closeDialog();
    }

    [RelayCommand]
    private void ConfirmInlinePairing() => inlinePairingDecision?.TrySetResult(true);

    [RelayCommand]
    private void DeclineInlinePairing() => inlinePairingDecision?.TrySetResult(false);

    [RelayCommand]
    private async Task StartSetupAsync()
    {
        operationCts?.Cancel();
        operationCts?.Dispose();
        operationCts = new CancellationTokenSource();
        var ct = operationCts.Token;
        try
        {
            ResetPanels();
            ShowWaitingPhonePanel = true;
            var discovered = await bluetoothPairingService.DiscoverAsync(activeDevice, ct);
            if (ct.IsCancellationRequested || !discovered) return;

            var paired = await bluetoothPairingService.PairAsync(activeDevice, PresentInlinePairingAsync, ct);
            if (ct.IsCancellationRequested || !paired) return;

            await SaveBluetoothDeviceId().ConfigureAwait(false);
            await dispatcher.EnqueueAsync(closeDialog);
        }
        finally
        {
            operationCts?.Dispose();
            operationCts = null;
        }
    }
    private async Task<bool> PresentInlinePairingAsync(string deviceName, string pin)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await dispatcher.EnqueueAsync(() =>
        {
            inlinePairingDecision = tcs;
            InlinePairingDeviceName = deviceName;
            InlinePairingCode = pin;
            ShowPairingPanel = false;
            ShowInlinePairingPanel = true;
        });

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            await dispatcher.EnqueueAsync(() =>
            {
                inlinePairingDecision = null;
                ShowInlinePairingPanel = false;
                ShowPairingPanel = true;
            });
        }
    }

    private void ApplyState(BluetoothPairingState s)
    {
        if (s.Status is BluetoothPairingStatus.Cancelled) return;

        if (s.Status is BluetoothPairingStatus.BluetoothRadioOff)
        {
            operationCts?.Cancel();
            operationCts?.Dispose();
            operationCts = null;
            ResetPanels();
            ShowBluetoothOffMessage = true;
            return;
        }

        if (s.Step is BluetoothPairingStep.Discovery && s.Status is BluetoothPairingStatus.InProgress)
        {
            ShowWaitingPhonePanel = false;
            ShowScanningPanel = true;
            ShowPairingPanel = false;
            return;
        }

        if (s.Step is BluetoothPairingStep.Discovery && s.Status is BluetoothPairingStatus.DeviceNotFound)
        {
            ShowWaitingPhonePanel = false;
            ShowScanningPanel = false;
            ShowPairingPanel = false;
            ShowDeviceNotFoundMessage = true;
            return;
        }

        if (s.Step is BluetoothPairingStep.Discovery && s.Status is BluetoothPairingStatus.PhoneDiscoveryRequestDenied)
        {
            SetDeniedState();
            return;
        }

        if (s.Step is BluetoothPairingStep.Pairing && s.Status is BluetoothPairingStatus.InProgress)
        {
            ShowScanningPanel = false;
            ShowWaitingPhonePanel = false;
            ShowPairingPanel = true;
            return;
        }

        if (s.Step is BluetoothPairingStep.Pairing && s.Status is BluetoothPairingStatus.PairingFailed)
        {
            ShowPairingPanel = false;
            ShowPairingFailedMessage = true;
        }
    }

    private void ResetPanels()
    {
        ShowIntroPanel = false;
        ShowPhonePermissionRetry = false;
        ShowWaitingPhonePanel = false;
        ShowScanningPanel = false;
        ShowBluetoothOffMessage = false;
        ShowDeviceNotFoundMessage = false;
        ShowPairingFailedMessage = false;
        ShowPairingPanel = false;
        ShowInlinePairingPanel = false;
    }

    private void SetDeniedState()
    {
        ResetPanels();
        ShowIntroPanel = true;
        ShowPhonePermissionRetry = true;
    }

    private async Task SaveBluetoothDeviceId()
    {
        var entity = await deviceRepository.GetPairedDevice(activeDevice.Id).ConfigureAwait(false);
        if (entity is null) return;
        if (string.IsNullOrEmpty(activeDevice.BluetoothAddress) || string.IsNullOrEmpty(activeDevice.BluetoothClassicDeviceId)) return;

        entity.BluetoothAddress = activeDevice.BluetoothAddress;
        entity.BluetoothClassicDeviceId = activeDevice.BluetoothClassicDeviceId;
        entity.CallsTransportDeviceId = null;
        activeDevice.CallsTransportDeviceId = null;
        deviceRepository.AddOrUpdateRemoteDevice(entity);
        Logger.Info($"Persisted calling Bluetooth for device {activeDevice.Id}: address={activeDevice.BluetoothAddress}, classicId={activeDevice.BluetoothClassicDeviceId}");
    }
}
