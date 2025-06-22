using System.Diagnostics.CodeAnalysis;

namespace Sefirah.Platforms.Windows.Helpers;
public class DirectoryEqualityComparer : IEqualityComparer<DirectoryInfo>
{
    public bool Equals(DirectoryInfo? x, DirectoryInfo? y)
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

    public int GetHashCode([DisallowNull] string obj) => GetHashCode(new DirectoryInfo(obj));
    public int GetHashCode([DisallowNull] DirectoryInfo obj) =>
        HashCode.Combine(
            // ignore sync attributes
            (int)obj.Attributes & ~SyncAttributes.ALL,
            obj.CreationTimeUtc,
            obj.LastWriteTimeUtc
        );
}
