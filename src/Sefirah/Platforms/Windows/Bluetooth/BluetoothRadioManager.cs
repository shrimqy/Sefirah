using Windows.Devices.Radios;

namespace Sefirah.Platforms.Windows.Bluetooth;

public sealed class BluetoothRadioManager(ILogger logger)
{
    private Radio? bluetoothRadio;

    public bool IsBluetoothSupported { get; private set; }

    public bool IsBluetoothRadioOn => bluetoothRadio?.State is RadioState.On;

    public RadioState RadioState => bluetoothRadio?.State ?? RadioState.Unknown;

    public event Action<RadioState>? RadioStateChanged;

    public async Task<bool> RefreshAsync()
    {
        try
        {
            if (bluetoothRadio is not null)
            {
                return true;
            }

            var radios = await Radio.GetRadiosAsync();
            bluetoothRadio = radios.FirstOrDefault(r => r.Kind is RadioKind.Bluetooth);
            if (bluetoothRadio is null)
            {
                IsBluetoothSupported = false;
                return false;
            }

            bluetoothRadio.StateChanged += OnBluetoothRadioStateChanged;
            IsBluetoothSupported = true;
            return true;
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to refresh bluetooth radio: {ex}");
            IsBluetoothSupported = false;
            return false;
        }
    }

    public Task<bool> TryEnableAsync() => TrySetEnabledAsync(true);

    public async Task<bool> TrySetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (!await RefreshAsync() || bluetoothRadio is null)
        {
            return false;
        }

        if (bluetoothRadio.State == (enabled ? RadioState.On : RadioState.Off))
        {
            return true;
        }

        try
        {
            var access = await Radio.RequestAccessAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (access is not RadioAccessStatus.Allowed)
            {
                logger.Debug($"Bluetooth radio access not allowed: {access}");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var setState = await bluetoothRadio.SetStateAsync(enabled ? RadioState.On : RadioState.Off);
            if (setState is not RadioAccessStatus.Allowed)
            {
                logger.Debug($"Bluetooth radio enable denied: {setState}");
                return false;
            }

            // SetStateAsync only accepts a request; wait for its observable final state.
            for (var attempt = 0; attempt < 25; attempt++)
            {
                if (bluetoothRadio.State == (enabled ? RadioState.On : RadioState.Off)) return true;
                await Task.Delay(200, cancellationToken);
            }
            return false;
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to enable bluetooth radio: {ex}");
            return false;
        }
    }

    private void OnBluetoothRadioStateChanged(Radio sender, object args) => RadioStateChanged?.Invoke(sender.State);
}
