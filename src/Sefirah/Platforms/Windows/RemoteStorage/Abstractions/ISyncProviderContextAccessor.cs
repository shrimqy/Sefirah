using Sefirah.Platforms.Windows.Abstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
public interface ISyncProviderContextAccessor
{
    SyncProviderContext Context { get; }
}
