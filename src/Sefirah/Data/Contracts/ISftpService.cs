using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface ISftpService
{
    Task InitializeAsync(PairedDevice device, SftpServerInfo info);

    void Remove(string deviceId);
}
