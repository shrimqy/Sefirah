using System.Diagnostics.CodeAnalysis;

namespace Sefirah.Platforms.Windows.Helpers;
public class FileEqualityComparer : IComparer<FileInfo>, IEqualityComparer<FileInfo>
{
    public int Compare(FileInfo? x, FileInfo? y)
    {
        ArgumentNullException.ThrowIfNull(x, nameof(x));
        ArgumentNullException.ThrowIfNull(y, nameof(y));
        return x.LastWriteTimeUtc.CompareTo(y.LastWriteTimeUtc);
    }

    public bool Equals(FileInfo? x, FileInfo? y)
    {
        if (x == y)
        {
            return true;
        }
        if (y is null || x is null)
        {
            return false;
        }
        return GetHashCode(x) == GetHashCode(y);
    }

    public int GetHashCode([DisallowNull] string obj) => GetHashCode(new FileInfo(obj));

    public int GetHashCode([DisallowNull] FileInfo obj) =>
        HashCode.Combine(
            obj.Length,
            obj.LastWriteTimeUtc
        );
}
