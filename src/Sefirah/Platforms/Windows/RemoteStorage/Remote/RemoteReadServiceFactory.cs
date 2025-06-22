using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Remote;
public class RemoteReadServiceFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteReadService>> options)
    : RemoteFactory<IRemoteReadService>(contextAccessor, options)
{ }
