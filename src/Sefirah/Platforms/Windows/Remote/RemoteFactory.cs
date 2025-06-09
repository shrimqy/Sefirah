using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.Remote;
public class RemoteFactory<T>(SyncProviderContextAccessor contextAccessor, IEnumerable<LazyRemote<T>> options)
{
    public T Create() =>
        options.Single(lazy => lazy.RemoteKind == contextAccessor.Context.RemoteKind).Value;
}
