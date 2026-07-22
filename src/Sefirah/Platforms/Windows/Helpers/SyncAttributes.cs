using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.Helpers;

/// <summary>
/// Cloud-files (Files On-Demand) attributes that <see cref="FileAttributes"/> is missing.
/// https://learn.microsoft.com/windows/win32/fileio/file-attribute-constants
/// </summary>
public static class SyncAttributes
{
    public const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    public const FileAttributes Pinned = (FileAttributes)0x00080000;

    public const FileAttributes Unpinned = (FileAttributes)0x00100000;

    public const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

    public const FileAttributes All =
        FileAttributes.Archive
        | FileAttributes.SparseFile
        | FileAttributes.ReparsePoint
        | FileAttributes.Offline
        | RecallOnOpen
        | Pinned
        | Unpinned
        | RecallOnDataAccess;
}

public static class FileAttributesExtensions
{
    public static bool IsPinned(this FileAttributes source) =>
        source.HasFlag(SyncAttributes.Pinned) && !source.HasFlag(SyncAttributes.Unpinned);


    public static bool IsDehydrationRequested(this FileAttributes source) =>
        source.HasFlag(SyncAttributes.Unpinned) && !source.HasFlag(SyncAttributes.Pinned);

    public static bool IsExcluded(this FileAttributes source) =>
        source.HasFlag(SyncAttributes.Pinned | SyncAttributes.Unpinned);
}
