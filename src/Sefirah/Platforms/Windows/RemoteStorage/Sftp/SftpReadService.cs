using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.RemoteStorage.Sftp;
public class SftpReadService(
    ISftpContextAccessor contextAccessor,
    SftpClient client,
    ILogger logger
) : IRemoteReadService
{
    protected readonly string[] _relativeDirectoryNames = [".", "..", "#Recycle"];
    protected readonly SftpContext _context = contextAccessor.Context;

    public bool Exists(string relativePath) =>
        client.Exists(GetSftpPath(relativePath));

    public bool IsDirectory(string relativePath) =>
        client.Get(GetSftpPath(relativePath)).IsDirectory;

    public IEnumerable<RemoteDirectoryInfo> EnumerateDirectories(string relativeDirectory) =>
        EnumerateDirectories(relativeDirectory, "*");

    public IEnumerable<RemoteDirectoryInfo> EnumerateDirectories(string relativeDirectory, string pattern)
    {
        if (!client.IsConnected)
        {
            return [];
        }

        if (pattern != "*")
        {
            throw new NotSupportedException($"Does not support pattern other than \"*\": {pattern}; try https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing");
        }
        return client.ListDirectory(GetSftpPath(relativeDirectory))
            .Where((sftpFile) => sftpFile.IsDirectory)
            .Where((sftpFile) => !_relativeDirectoryNames.Contains(sftpFile.Name))
            .Where(sftpFile => !FileHelper.IsSystemDirectory(PathMapper.GetRelativePath(sftpFile.FullName, _context.Directory)))
            .Select(GetRemoteDirectoryInfo)
            .ToArray();
    }

    public IEnumerable<RemoteFileInfo> EnumerateFiles(string relativeDirectory) =>
        EnumerateFiles(relativeDirectory, "*");

    public IEnumerable<RemoteFileInfo> EnumerateFiles(string relativeDirectory, string pattern)
    {
        if (!client.IsConnected)
        {
            return [];
        }
        try
        {
        if (pattern != "*")
        {
            throw new NotSupportedException($"Does not support pattern other than \"*\": {pattern}; try https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing");
        }
        return client.ListDirectory(GetSftpPath(relativeDirectory))
            .Where((sftpFile) => !sftpFile.IsDirectory && sftpFile.IsRegularFile)
            .Select(GetRemoteFileInfo)
            .ToArray();
    }
        catch (SshConnectionException)
        {
            return [];
        }
    }

    public RemoteDirectoryInfo GetDirectoryInfo(string relativeDirectory)
    {
        var sftpFile = client.Get(GetSftpPath(relativeDirectory));
        return GetRemoteDirectoryInfo(sftpFile);
    }

    public RemoteFileInfo GetFileInfo(string relativeFile)
    {
        var sftpFile = client.Get(GetSftpPath(relativeFile));
        return GetRemoteFileInfo(sftpFile);
    }

    public Task<Stream> GetFileStream(string relativeFile)
    {
        SftpFileStream sftpStream = client.Open(GetSftpPath(relativeFile), FileMode.Open);
        return Task.FromResult((Stream)sftpStream);
    }

    protected string GetSftpPath(string relativePath) =>
        Path.Join(_context.Directory, relativePath).Replace(@"\", "/");

    private RemoteDirectoryInfo GetRemoteDirectoryInfo(ISftpFile sftpFile) =>
        new()
        {
            Name = sftpFile.Name,
            Attributes = FileAttributes.Directory,
            RelativePath = PathMapper.GetRelativePath(sftpFile.FullName, _context.Directory),
            RelativeParentDirectory = PathMapper.GetRelativePath(sftpFile.FullName, _context.Directory).Replace($"/{sftpFile.Name}", ""),
            CreationTimeUtc = sftpFile.LastWriteTimeUtc,
            LastWriteTimeUtc = sftpFile.LastWriteTimeUtc,
            LastAccessTimeUtc = sftpFile.LastAccessTimeUtc,
        };

    private RemoteFileInfo GetRemoteFileInfo(ISftpFile sftpFile) =>
        new()
        {
            Name = sftpFile.Name,
            Length = sftpFile.Length,
            Attributes = FileAttributes.None,
            RelativePath = PathMapper.GetRelativePath(sftpFile.FullName, _context.Directory),
            RelativeParentDirectory = PathMapper.GetRelativePath(sftpFile.FullName, _context.Directory).Replace($"/{sftpFile.Name}", ""),
            CreationTimeUtc = sftpFile.LastWriteTimeUtc,
            LastWriteTimeUtc = sftpFile.LastWriteTimeUtc,
            LastAccessTimeUtc = sftpFile.LastAccessTimeUtc,
        };
}
