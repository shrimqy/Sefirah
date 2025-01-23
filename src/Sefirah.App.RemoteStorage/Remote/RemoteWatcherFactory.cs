using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.RemoteAbstractions;

namespace Sefirah.App.RemoteStorage.Remote;
public class RemoteWatcherFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteWatcher>> options)
    : RemoteFactory<IRemoteWatcher>(contextAccessor, options)
{ }
