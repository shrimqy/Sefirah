using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.Helpers;
public static class SyncAttributes
{
    public const int OFFLINE = 4096;
    public const int RECALL_ON_OPEN = 262144;
    public const int PINNED = 524288;
    public const int UNPINNED = 1048576;
    /// <summary>
    /// Not fully present locally
    /// </summary>
    public const int RECALL_ON_DATA_ACCESS = 4194304;
    public const int ALL =
        (int)FileAttributes.Archive
        | (int)FileAttributes.SparseFile
        | (int)FileAttributes.ReparsePoint
        | OFFLINE
        | RECALL_ON_OPEN
        | PINNED
        | UNPINNED
        | RECALL_ON_DATA_ACCESS;
    public static bool HasAllFlags(FileAttributes source, int flags) =>
        HasAllFlags((int)source, flags);
    public static bool HasAllFlags(int source, int flags) =>
        (source & flags) == flags;
    public static bool HasAnyFlag(FileAttributes source, int flag) =>
        HasAnyFlag((int)source, flag);
    public static bool HasAnyFlag(int source, int flag) =>
        (source & flag) != 0;
    public static int Take(int source, int take, int mask = ALL) =>
        source & ~mask | take & mask;
}

public static class FileAttributesExtensions
{
    public static bool HasAllSyncFlags(this FileAttributes source, int flags) =>
        SyncAttributes.HasAllFlags(source, flags);
    public static bool HasAnySyncFlag(this FileAttributes source, int flag) =>
        SyncAttributes.HasAnyFlag(source, flag);
    public static int Take(this FileAttributes source, FileAttributes take, int mask = SyncAttributes.ALL) =>
        SyncAttributes.Take((int)source, (int)take, mask);
}
