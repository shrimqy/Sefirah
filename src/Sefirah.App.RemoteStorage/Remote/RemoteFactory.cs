using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.RemoteAbstractions;

namespace Sefirah.App.RemoteStorage.Remote;
public class RemoteFactory<T>(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<T>> options)
{
    public T Create() =>
        options.Single(lazy => lazy.RemoteKind == contextAccessor.Context.RemoteKind).Value;
}
