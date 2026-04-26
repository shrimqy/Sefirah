using Sefirah.Data.Models;
using Windows.Devices.Enumeration;

namespace Sefirah.Platforms.Desktop.Bluetooth;

public sealed class BluetoothPairingService : IBluetoothPairingService
{
    public bool IsBluetoothSupported => false;

    public bool IsBluetoothRadioOn => false;

    public BluetoothPairingState State => new(BluetoothPairingStep.Connectivity, BluetoothPairingStatus.NotStarted);

    public event EventHandler<BluetoothPairingState>? StateChanged;

    public void Stop() { }

    public Task<bool> DiscoverAsync(PairedDevice phone, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> PairAsync(PairedDevice phone, Func<string, string, Task<bool>> confirmAsync, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> TryEnableBluetoothAsync() => Task.FromResult(false);

    public Task<DeviceUnpairingResultStatus> UnpairAsync(string deviceId) => Task.FromResult(DeviceUnpairingResultStatus.Failed);

    public void HandleBluetoothPairingResult(PairedDevice device, BluetoothPairingResult result) { }
}
