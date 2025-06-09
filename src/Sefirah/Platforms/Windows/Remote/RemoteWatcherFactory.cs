using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.Remote;
public class RemoteWatcherFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteWatcher>> options)
    : RemoteFactory<IRemoteWatcher>(contextAccessor, options)
{ }
