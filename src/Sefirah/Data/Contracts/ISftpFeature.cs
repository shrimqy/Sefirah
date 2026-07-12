using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface ISftpFeature : IFeature
{
    Task InitializeAsync(PairedDevice device, SftpServerInfo info);

    void Remove(string deviceId);

    void RemoveAll();
}
