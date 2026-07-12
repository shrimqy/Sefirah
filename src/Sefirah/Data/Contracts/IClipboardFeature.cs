using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IClipboardFeature : IFeature
{
    /// <summary>
    /// Sets the content of the clipboard.
    /// </summary>
    Task SetContentAsync(object content, PairedDevice sourceDevice);
}
