namespace Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
public class LazyRemote<T>(Func<T> valueFactory, string remoteKind) : Lazy<T>(valueFactory)
{
    public string RemoteKind => remoteKind;
}
