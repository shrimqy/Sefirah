using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.RemoteAbstractions;

namespace Sefirah.App.RemoteStorage.Remote;
public class RemoteReadWriteServiceFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteReadWriteService>> options)
    : RemoteFactory<IRemoteReadWriteService>(contextAccessor, options)
{ }
