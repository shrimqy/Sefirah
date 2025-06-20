namespace Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
public interface IRemoteReadWriteService : IRemoteReadService
{
    Task CreateFile(FileInfo sourceFileInfo, string relativeFile);
    Task UpdateFile(FileInfo sourceFileInfo, string relativeFile);
    void MoveFile(string oldRelativeFile, string newRelativeFile);
    void DeleteFile(string relativeFile);
    Task CreateDirectory(DirectoryInfo sourceDirectoryInfo, string relativeDirectory);
    Task UpdateDirectory(DirectoryInfo sourceDirectoryInfo, string relativeDirectory);
    void MoveDirectory(string oldRelativeDirectory, string newRelativeDirectory);
    void DeleteDirectory(string relativeDirectory);
}
