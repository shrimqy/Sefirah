using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.Remote;
public class RemoteReadServiceFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteReadService>> options)
    : RemoteFactory<IRemoteReadService>(contextAccessor, options)
{ }
