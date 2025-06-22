using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Remote;
public class RemoteFactory<T>(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<T>> options)
{
    public T Create() =>
        options.Single(lazy => lazy.RemoteKind == contextAccessor.Context.RemoteKind).Value;
}
