namespace Sefirah.App.RemoteStorage.RemoteAbstractions;
public record RemoteFileInfo : RemoteFileSystemInfo
{
    public required long Length { get; init; }
    public override int GetHashCode() =>
        HashCode.Combine(Length, LastWriteTimeUtc);
}
