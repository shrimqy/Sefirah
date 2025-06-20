namespace Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
public interface IRemoteReadService
{
    IEnumerable<RemoteDirectoryInfo> EnumerateDirectories(string relativeDirectory);
    IEnumerable<RemoteDirectoryInfo> EnumerateDirectories(string relativeDirectory, string pattern);
    IEnumerable<RemoteFileInfo> EnumerateFiles(string relativeDirectory);
    IEnumerable<RemoteFileInfo> EnumerateFiles(string relativeDirectory, string pattern);
    bool Exists(string relativePath);
    bool IsDirectory(string relativePath);
    RemoteDirectoryInfo GetDirectoryInfo(string relativeDirectory);
    RemoteFileInfo GetFileInfo(string relativeFile);
    Task<Stream> GetFileStream(string relativeFile);
}
