using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IClipboardFeature : IFeature
{
    /// <summary>
    /// Raised once a new item from the device has been added to the history.
    /// </summary>
    event EventHandler<PairedDevice>? HistoryChanged;

    /// <summary>
    /// Sets the clipboard from a remote
    /// </summary>
    Task SetContentAsync(ClipboardInfo clipboard, PairedDevice sourceDevice);

    /// <summary>
    /// Sends the current local clipboard content to the specified device.
    /// </summary>
    void SendToDevice(PairedDevice device);
}
