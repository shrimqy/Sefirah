using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface ISftpFeature : IFeature
{
    /// <summary>
    /// Starts device storage access for the platform (Linux GVfs/sshfs mount, or Windows sync-root registration).
    /// </summary>
    Task Mount(PairedDevice device, SftpServerInfo info);

    /// <summary>
    /// Opens the device filesystem via the preferred local mount/sync path when available.
    /// </summary>
    Task BrowseAsync(PairedDevice device);

    /// <summary>
    /// Launches an sftp:// URI for an external client and copies it to the clipboard.
    /// </summary>
    Task BrowseUriAsync(PairedDevice device);

    /// <summary>
    /// Raised when a device announces an SFTP endpoint. The device generates fresh credentials every
    /// time its server restarts, so anything holding a connection has to rebuild it.
    /// </summary>
    event EventHandler<PairedDevice>? SessionChanged;

    /// <summary>
    /// Returns the SFTP endpoint the device is currently serving, or null when it has not announced one.
    /// </summary>
    SftpSession? GetSession(string deviceId);

    void Remove(string deviceId);

    Task RemoveAll();
}
