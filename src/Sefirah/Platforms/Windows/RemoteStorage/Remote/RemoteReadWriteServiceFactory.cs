using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Remote;
public class RemoteReadWriteServiceFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteReadWriteService>> options)
    : RemoteFactory<IRemoteReadWriteService>(contextAccessor, options)
{ }
