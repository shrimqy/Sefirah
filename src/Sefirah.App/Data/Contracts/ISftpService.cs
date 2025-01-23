using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;

public interface ISftpService
{
    /// <summary>
    /// Initializes the sftp service with the server information and shell services.
    /// </summary>
    Task InitializeAsync(SftpServerInfo info);
}
