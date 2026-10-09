using Renci.SshNet;
using Renci.SshNet.Sftp;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Sftp;

#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.

public class SftpReadWriteService(
    ISftpContextAccessor contextAccessor,
    SftpClient client,
    ILogger logger) : SftpReadService(contextAccessor, client, logger), IRemoteReadWriteService
{
#pragma warning restore CS9107
    public async Task CreateFile(FileInfo sourceFileInfo, string relativeFile)
    {

        var serverFile = GetSftpPath(relativeFile);

        if (Exists(relativeFile))
        {
            return;
        }

        logger.Debug($"Create File {relativeFile}");
        EnsureRemoteParentExists(serverFile);

        using (var sourceStream = await FileHelper.WaitUntilUnlocked(sourceFileInfo.OpenRead, logger))
        {
            await Task.Factory.FromAsync(client.BeginUploadFile(sourceStream, serverFile), client.EndUploadFile);
        }

        var sftpFile = client.Get(serverFile);
        sftpFile.LastWriteTimeUtc = sourceFileInfo.LastWriteTimeUtc;
        sftpFile.LastAccessTimeUtc = sourceFileInfo.LastAccessTimeUtc;
        sftpFile.UpdateStatus();
    }

    public async Task UpdateFile(FileInfo sourceFileInfo, string relativeFile)
    {
        var serverFile = GetSftpPath(relativeFile);

        using (var sourceStream = await FileHelper.WaitUntilUnlocked(sourceFileInfo.OpenRead, logger))
        {
            await Task.Factory.FromAsync(client.BeginUploadFile(sourceStream, serverFile), client.EndUploadFile);
        }

        var sftpFile = client.Get(serverFile);
        sftpFile.LastWriteTimeUtc = sourceFileInfo.LastWriteTimeUtc;
        sftpFile.LastAccessTimeUtc = sourceFileInfo.LastAccessTimeUtc;
        sftpFile.UpdateStatus();
    }

    public void MoveFile(string oldRelativeFile, string newRelativeFile)
    {
        var oldServerFile = GetSftpPath(oldRelativeFile);
        var newServerFile = GetSftpPath(newRelativeFile);

        var sftpFile = client.Get(oldServerFile);
        sftpFile.MoveTo(newServerFile);
    }

    public void DeleteFile(string relativeFile)
    {
        var serverFile = GetSftpPath(relativeFile);
        logger.Debug($"Delete File {serverFile}");
        client.DeleteFile(serverFile);
    }

    public Task CreateDirectory(DirectoryInfo sourceDirectoryInfo, string relativeDirectory)
    {
        var serverDirectory = GetSftpPath(relativeDirectory);

        if (Exists(relativeDirectory))
        {
            return Task.CompletedTask;
        }

        client.CreateDirectory(serverDirectory);
        var sftpFile = client.Get(serverDirectory);
        sftpFile.LastWriteTimeUtc = sourceDirectoryInfo.LastWriteTimeUtc;
        sftpFile.LastAccessTimeUtc = sourceDirectoryInfo.LastAccessTimeUtc;
        sftpFile.UpdateStatus();

        return Task.CompletedTask;
    }

    public Task UpdateDirectory(DirectoryInfo sourceDirectoryInfo, string relativeDirectory)
    {
        throw new NotImplementedException();
    }

    public void MoveDirectory(string oldRelativeDirectory, string newRelativeDirectory)
    {
        var oldServerDirectory = GetSftpPath(oldRelativeDirectory);
        var newServerDirectory = GetSftpPath(newRelativeDirectory);
        logger.Debug($"Move Directory {oldServerDirectory} -> {newServerDirectory}");
        var sftpFile = client.Get(oldServerDirectory);
        sftpFile.MoveTo(newServerDirectory);
    }

    public void DeleteDirectory(string relativeDirectory)
    {
        var serverDirectory = GetSftpPath(relativeDirectory);
        logger.Debug($"Delete Directory {serverDirectory}");

        foreach (ISftpFile sftpFile in client.ListDirectory(serverDirectory))
        {
            if (_relativeDirectoryNames.Contains(sftpFile.Name))
            {
                continue;
            }
            if (sftpFile.IsDirectory)
            {
                var relativePath = PathMapper.GetRelativePath(sftpFile.FullName, _context.Directory);
                DeleteDirectory(relativePath);
            }
            else
            {
                sftpFile.Delete();
            }
        }

        client.DeleteDirectory(serverDirectory);
    }

    /// <summary>Creates any missing parent directories of <paramref name="serverPath"/> on the remote.</summary>
    private void EnsureRemoteParentExists(string serverPath)
    {
        var separatorIndex = serverPath.LastIndexOf('/');
        if (separatorIndex <= 0)
        {
            return;
        }
        var parent = serverPath[..separatorIndex];
        if (client.Exists(parent))
        {
            return;
        }
        EnsureRemoteParentExists(parent);
        client.CreateDirectory(parent);
    }
}
