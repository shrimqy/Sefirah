using Sefirah.Data.Models;
using Windows.Devices.Enumeration;

namespace Sefirah.Data.Contracts;

public interface IBluetoothPairingService
{
    bool IsBluetoothSupported { get; }
    bool IsBluetoothRadioOn { get; }

    BluetoothPairingState State { get; }

    event EventHandler<BluetoothPairingState>? StateChanged;

    void Stop();

    Task<bool> DiscoverAsync(PairedDevice phone, CancellationToken cancellationToken = default);

    Task<bool> PairAsync(PairedDevice phone, Func<string, string, Task<bool>> confirmAsync, CancellationToken cancellationToken = default);

    Task<bool> TryEnableBluetoothAsync();

    Task<DeviceUnpairingResultStatus> UnpairAsync(string deviceId);

    void HandleBluetoothPairingResult(PairedDevice device, BluetoothPairingResult result);
}
