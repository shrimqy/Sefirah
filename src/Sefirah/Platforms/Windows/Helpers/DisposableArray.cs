namespace Sefirah.Platforms.Windows.Helpers;
public class DisposableArray<T>(T[] source)
    : Disposable<T[]>(source, (items) => { foreach (var item in items) { item.Dispose(); } })
    where T : IDisposable
{ }

public static partial class ArrayExtensions
{
    public static DisposableArray<T> ToDisposableArray<T>(this IEnumerable<T> source) where T : IDisposable =>
        new(source.ToArray());
}
