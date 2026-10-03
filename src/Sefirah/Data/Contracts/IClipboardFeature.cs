using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IClipboardFeature : IFeature
{
    /// <summary>
    /// Sets the clipboard from a remote clipboard message.
    /// </summary>
    Task SetContentAsync(ClipboardInfo clipboard, PairedDevice sourceDevice);

    /// <summary>
    /// Sets the clipboard from content (e.g. text or <see cref="StorageFile"/>).
    /// </summary>
    Task SetContentAsync(object content, PairedDevice sourceDevice);

    /// <summary>
    /// Sends the current local clipboard content to the specified device.
    /// </summary>
    void SendToDevice(PairedDevice device);
}
