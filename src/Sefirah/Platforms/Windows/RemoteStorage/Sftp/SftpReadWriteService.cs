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

        await Upload(sourceFileInfo, serverFile);
    }

    public Task UpdateFile(FileInfo sourceFileInfo, string relativeFile) =>
        Upload(sourceFileInfo, GetSftpPath(relativeFile));

    /// <summary>Uploads to a temporary file, then renames it over <paramref name="serverFile"/>.</summary>
    private async Task Upload(FileInfo sourceFileInfo, string serverFile)
    {
        var separatorIndex = serverFile.LastIndexOf('/');
        var tempFile = $"{serverFile[..(separatorIndex + 1)]}.{serverFile[(separatorIndex + 1)..]}{SftpConstants.PartialFileSuffix}";
        try
        {
            using (var sourceStream = await FileHelper.WaitUntilUnlocked(sourceFileInfo.OpenRead, logger))
            {
                await Task.Factory.FromAsync(client.BeginUploadFile(sourceStream, tempFile), client.EndUploadFile);
            }
            client.RenameFile(tempFile, serverFile, isPosix: true);
        }
        catch
        {
            try
            {
                client.DeleteFile(tempFile);
            }
            catch (Exception) { }
            throw;
        }

        var sftpFile = client.Get(serverFile);
        sftpFile.LastWriteTimeUtc = sourceFileInfo.LastWriteTimeUtc;
        sftpFile.LastAccessTimeUtc = sourceFileInfo.LastAccessTimeUtc;
        sftpFile.UpdateStatus();
    }

    public void MoveFile(string oldRelativeFile, string newRelativeFile) =>
        Move(GetSftpPath(oldRelativeFile), GetSftpPath(newRelativeFile));

    private void Move(string oldServerPath, string newServerPath)
    {
        EnsureRemoteParentExists(newServerPath);
        // The device storage ignores case, so renaming to another case of the same name does nothing
        if (string.Equals(oldServerPath, newServerPath, StringComparison.OrdinalIgnoreCase))
        {
            var tempPath = oldServerPath + SftpConstants.PartialFileSuffix;
            client.RenameFile(oldServerPath, tempPath);
            client.RenameFile(tempPath, newServerPath);
            return;
        }
        client.RenameFile(oldServerPath, newServerPath);
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
        Move(oldServerDirectory, newServerDirectory);
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
