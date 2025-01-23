using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.RemoteAbstractions;

namespace Sefirah.App.RemoteStorage.Remote;
public class RemoteReadServiceFactory(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<IRemoteReadService>> options)
    : RemoteFactory<IRemoteReadService>(contextAccessor, options)
{ }
