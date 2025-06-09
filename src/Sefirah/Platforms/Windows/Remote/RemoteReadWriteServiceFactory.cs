using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.Remote;
public class RemoteReadWriteServiceFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteReadWriteService>> options)
    : RemoteFactory<IRemoteReadWriteService>(contextAccessor, options)
{ }
