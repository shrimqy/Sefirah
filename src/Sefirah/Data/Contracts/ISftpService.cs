using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;
public interface ISftpService
{
    /// <summary>
    /// Initializes the sftp service with the server information and shell services.
    /// </summary>
    Task InitializeAsync(PairedDevice device, SftpServerInfo info);

    /// <summary>
    /// Removes the sync root.
    /// </summary>
    void Remove(string deviceId);
}
