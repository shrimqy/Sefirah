using System.Diagnostics.CodeAnalysis;

namespace Sefirah.Platforms.Windows.Helpers;

public class FileEqualityComparer : IComparer<FileInfo>, IEqualityComparer<FileInfo>
{
    public int Compare(FileInfo? x, FileInfo? y)
    {
        ArgumentNullException.ThrowIfNull(x, nameof(x));
        ArgumentNullException.ThrowIfNull(y, nameof(y));
        return NormalizeToUtcSeconds(x.LastWriteTimeUtc).CompareTo(NormalizeToUtcSeconds(y.LastWriteTimeUtc));
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
        GetHashCode(obj.Length, obj.LastWriteTimeUtc);

    public static int GetHashCode(long length, DateTime lastWriteTimeUtc) =>
        HashCode.Combine(length, NormalizeToUtcSeconds(lastWriteTimeUtc));

    /// <summary>
    /// SFTP stores mtime as whole Unix seconds,
    /// so sub-second precision is lost on a round-trip. Compare at second resolution to match.
    /// </summary>
    internal static DateTime NormalizeToUtcSeconds(DateTime lastWriteTimeUtc)
    {
        var utc = lastWriteTimeUtc.Kind switch
        {
            DateTimeKind.Utc => lastWriteTimeUtc,
            DateTimeKind.Local => lastWriteTimeUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(lastWriteTimeUtc, DateTimeKind.Utc),
        };
        var ticks = utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond;
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}
