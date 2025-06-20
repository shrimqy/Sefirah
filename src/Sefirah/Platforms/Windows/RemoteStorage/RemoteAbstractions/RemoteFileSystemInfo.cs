using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
public record RemoteFileSystemInfo
{
    public required string Name { get; init; }
    public required FileAttributes Attributes { get; init; }
    public required string RelativePath { get; init; }
    public required string RelativeParentDirectory { get; init; }
    public required DateTime CreationTimeUtc { get; init; }
    public required DateTime LastWriteTimeUtc { get; init; }
    public required DateTime LastAccessTimeUtc { get; init; }
}
