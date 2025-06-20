using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Remote;
public class RemoteWatcherFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteWatcher>> options)
    : RemoteFactory<IRemoteWatcher>(contextAccessor, options)
{ }
