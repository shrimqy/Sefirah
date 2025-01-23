namespace Sefirah.App.RemoteStorage.Helpers;
public class Disposable<T>(T source, Action<T> dispose) : Disposable(() => dispose(source))
{
    public T Source => source;

    public static implicit operator T(Disposable<T> safeOf) => safeOf.Source;
}
