namespace Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
public record RemoteFileInfo : RemoteFileSystemInfo
{
    public required long Length { get; init; }
    public override int GetHashCode() =>
        HashCode.Combine(Length, LastWriteTimeUtc);
}
