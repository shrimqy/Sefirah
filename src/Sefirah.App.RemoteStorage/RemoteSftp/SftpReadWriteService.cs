using Renci.SshNet;
using Renci.SshNet.Sftp;
using Sefirah.App.RemoteStorage.Helpers;
using Sefirah.App.RemoteStorage.RemoteAbstractions;
using Sefirah.Common.Utils;

namespace Sefirah.App.RemoteStorage.RemoteSftp;
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

        if (Exists(serverFile))
        {
            throw new Exception("Conflict: already exists???");
        }

        logger.Debug("Create File {relativeFile}", relativeFile);
        PathMapper.EnsureSubDirectoriesExist(serverFile);

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
        // Update only - CreateFile to create!
        if (!Exists(serverFile))
        {
            return;
        }

        var sftpFile = client.Get(serverFile);
        if (sourceFileInfo.LastWriteTimeUtc < sftpFile.LastWriteTimeUtc)
        {
            return;
        }

        logger.Debug("Update File {relativeFile}", relativeFile);
        using (var sourceStream = sourceFileInfo.OpenRead())
        {
            await Task.Factory.FromAsync(client.BeginUploadFile(sourceStream, serverFile), client.EndUploadFile);
        }

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
        logger.Debug("Delete File {file}", serverFile);
        client.DeleteFile(serverFile);
        DeleteDirectoryIfEmpty(serverFile);
    }

    public Task CreateDirectory(DirectoryInfo sourceDirectoryInfo, string relativeDirectory)
    {
        var serverDirectory = GetSftpPath(relativeDirectory);

        if (Exists(serverDirectory))
        {
            throw new Exception("Conflict: already exists");
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
        logger.Debug("Move Directory {old} -> {new}", oldServerDirectory, newServerDirectory);
        var sftpFile = client.Get(oldServerDirectory);
        sftpFile.MoveTo(newServerDirectory);
    }

    public void DeleteDirectory(string relativeDirectory)
    {
        var serverDirectory = GetSftpPath(relativeDirectory);
        logger.Debug("Delete Directory {directory}", serverDirectory);

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

        DeleteDirectoryIfEmpty(serverDirectory);
    }

    private void DeleteDirectoryIfEmpty(string serverPath)
    {
        var serverDirectory = Path.GetDirectoryName(serverPath)!.Replace(@"\", "/");
        var root = GetSftpPath("");
        if (serverDirectory == GetSftpPath(""))
        {
            return;
        }
        if (!client.Exists(serverDirectory))
        {
            return;
        }
        var hasEntries = client.ListDirectory(serverDirectory)
            .Where(x => !_relativeDirectoryNames.Contains(x.Name))
            .Any();
        if (!hasEntries)
        {
            DeleteDirectory(PathMapper.GetRelativePath(serverDirectory, _context.Directory));
        }
    }
}
