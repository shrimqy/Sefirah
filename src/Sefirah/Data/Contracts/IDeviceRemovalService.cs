using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IDeviceRemovalService
{
    /// <summary>
    /// Unpairs from this PC: tells the device it was unpaired (when connected) so it drops the
    /// pairing too, then removes the device and its data.
    /// </summary>
    Task UnpairAsync(PairedDevice device);

    /// <summary>The device unpaired this PC: removes it and its data without notifying it back.</summary>
    Task HandleRemoteUnpairAsync(PairedDevice device);
}
