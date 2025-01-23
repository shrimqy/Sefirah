using Renci.SshNet;
using Renci.SshNet.Common;
using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.Helpers;
using Sefirah.App.RemoteStorage.RemoteAbstractions;
using Sefirah.Common.Utils;

namespace Sefirah.App.RemoteStorage.RemoteSftp;
public sealed class SftpWatcher(
    ISyncProviderContextAccessor syncContextAccessor,
    ISftpContextAccessor contextAccessor,
    SftpClient client,
    ILogger logger
) : IRemoteWatcher
{
    private readonly SyncProviderContext _syncContext = syncContextAccessor.Context;
    private readonly SftpContext _context = contextAccessor.Context;
    private readonly string[] _relativeDirectoryNames = [".", "..", "#Recycle"];
    private Dictionary<string, DateTime> _knownFiles = [];
    private bool _running = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public event RemoteCreateHandler? Created;
    public event RemoteChangeHandler? Changed;
    public event RemoteRenameHandler? Renamed;
    public event RemoteDeleteHandler? Deleted;

    public async void Start(CancellationToken stoppingToken = default)
    {
        ObjectDisposedException.ThrowIf(_cancellationTokenSource.IsCancellationRequested, this);
        if (_running)
        {
            throw new Exception("Already running");
        }
        _running = true;

        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        while (!linkedTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await TryReconnectAsync(linkedTokenSource.Token);
                    continue;
                }

                var foundFiles = IsHydrated(_context.Directory)
                                ? FindFiles(_context.Directory)
                                : [];

                var removedFiles = _knownFiles.Keys.Except(foundFiles.Keys).ToArray();
                foreach (var removedFile in removedFiles)
                {
                    Deleted?.Invoke(removedFile);
                }

                var addedFiles = foundFiles.Keys.Except(_knownFiles.Keys).ToArray();
                foreach (var addedFile in addedFiles)
                {
                    Created?.Invoke(addedFile);
                }

                var updatedFiles = foundFiles
                    .Where((pair) => _knownFiles.ContainsKey(pair.Key) && _knownFiles[pair.Key] < pair.Value)
                    .Select(pair => pair.Key)
                    .ToArray();
                foreach (var updatedFile in updatedFiles)
                {
                    Changed?.Invoke(updatedFile);
                }

                _knownFiles = foundFiles;

                try
                {
                    // Wait until next scan
                    await Task.Delay(_context.WatchPeriodSeconds * 1000, linkedTokenSource.Token);
                }
                catch (TaskCanceledException) { }
            }
            catch (SshConnectionException ex)
            {
                logger.Error("SSH connection error", ex);
                await TryReconnectAsync(linkedTokenSource.Token);
            }
            catch (Exception ex)
            {
                logger.Error("Unexpected error in SFTP watcher", ex);
                await Task.Delay(TimeSpan.FromSeconds(5), linkedTokenSource.Token);
            }
        }
    }

    private Dictionary<string, DateTime> FindFiles(string directory)
    {
        if (!client.IsConnected)
        {
            return [];  
        }
        try
        {
            var sftpFiles = client.ListDirectory(directory);

            // Get all hydrated subdirectories and recursively get their files
            var subFiles = sftpFiles
                .Where(sftpFile => sftpFile.IsDirectory && !_relativeDirectoryNames.Contains(sftpFile.Name))
                .Where(sftpFile => IsHydrated(sftpFile.FullName))
                .SelectMany(sftpFile => FindFiles(sftpFile.FullName))
                .ToArray();

            // Get current directory's files and hydrated directories
            var files = sftpFiles
                .Where(sftpFile => sftpFile.IsRegularFile || 
                                (sftpFile.IsDirectory && 
                                !_relativeDirectoryNames.Contains(sftpFile.Name) && 
                                IsHydrated(sftpFile.FullName)))
                .ToDictionary(
                    sftpFile => sftpFile.FullName,
                    sftpFile => sftpFile.IsDirectory ? DateTime.MaxValue : sftpFile.LastWriteTimeUtc
                );

            return subFiles.Concat(files).ToDictionary();
        }
        catch (SshConnectionException)
        {
            return [];
        }
    }

    private async Task TryReconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            client.Disconnect();
        }
        catch (Exception ex) 
        { 
            logger.Error("Error disconnecting SFTP client", ex);
        }

        try
        {
            client.Connect();
        }
        catch (Exception ex)
        {
            logger.Error("Failed to connect SFTP client", ex);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private bool IsHydrated(string serverPath)
    {
        serverPath = PathMapper.NormalizePath(serverPath);
        try
        {
            var relativePath = PathMapper.GetRelativePath(serverPath, _context.Directory);
            var clientPath = Path.Join(_syncContext.RootDirectory, relativePath);

            return Path.Exists(clientPath) &&
                   !File.GetAttributes(clientPath).HasAnySyncFlag(SyncAttributes.OFFLINE);
        }
        catch (Exception ex)
        {
            logger.Error("Error checking hydration state for {path}", serverPath, ex);
            return false;
        }
    }

    public void Dispose()
    {
        logger.Debug("Disposing SFTP watcher");
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
