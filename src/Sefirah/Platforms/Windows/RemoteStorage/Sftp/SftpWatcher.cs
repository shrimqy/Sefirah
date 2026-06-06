using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Sftp;
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

        try
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _cancellationTokenSource.Token);
            while (!linkedTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (!client.IsConnected)
                    {
                        await TryReconnectAsync(linkedTokenSource.Token);
                        if (!client.IsConnected)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), linkedTokenSource.Token);
                            continue;
                        }
                    }

                    var foundFiles = IsHydrated("") ? FindFiles("") : [];

                    if (client.IsConnected)
                    {
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
                    }

                    try
                    {
                        await Task.Delay(_context.WatchPeriodSeconds * 1000, linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SshConnectionException ex)
                {
                    logger.LogError("SSH connection error", ex);
                }
                catch (Exception ex)
                {
                    logger.LogError("Unexpected error in SFTP watcher", ex);
                    await Task.Delay(TimeSpan.FromSeconds(5), linkedTokenSource.Token);
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    private Dictionary<string, DateTime> FindFiles(string relativeDirectory)
    {
        if (client is null || !client.IsConnected)
        {
            throw new SshConnectionException("SFTP client is not connected");
        }

        var serverDirectory = Path.Join(_context.Directory, relativeDirectory).Replace(@"\", "/");
        var sftpFiles = client.ListDirectory(serverDirectory);

            var dirEntries = sftpFiles
                .Where(sftpFile => sftpFile.IsDirectory &&
                                   !_relativeDirectoryNames.Contains(sftpFile.Name) &&
                                   !FileHelper.IsSystemDirectory(PathMapper.GetRelativePath(sftpFile.FullName, _context.Directory)))
                .ToArray();

            var directories = dirEntries.ToDictionary(
                dir => PathMapper.GetRelativePath(dir.FullName, _context.Directory),
                _ => DateTime.MaxValue
            );

            // Get files from current directory
            var files = sftpFiles
                .Where(sftpFile => sftpFile.IsRegularFile)
                .ToDictionary(
                    file => PathMapper.GetRelativePath(file.FullName, _context.Directory),
                    file => file.LastWriteTimeUtc
                );

            // Recursively get files from hydrated subdirectories
            var subFiles = directories.Keys
                .Where(IsHydrated)
                .SelectMany(FindFiles)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            return directories
                .Concat(files)
                .Concat(subFiles)
                .ToDictionary();
        }

    private async Task TryReconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (client?.IsConnected ?? false)
            {
                try
                {
                    client.Disconnect();
                }
                catch (Exception ex) 
                { 
                    logger.LogError("Error disconnecting SFTP client", ex);
                }
            }

            if (client != null)
            {
                try
                {
                    client.Connect();
                    logger.LogInformation("Successfully reconnected to SFTP server");
                }
                catch (Exception ex)
                {
                    logger.LogError("Failed to connect SFTP client", ex);
                    // Check cancellation before delay
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            else
            {
                logger.LogError("SFTP client is null during reconnection attempt");
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Unexpected error during reconnection", ex);
            throw;
        }
    }

    private bool IsHydrated(string relativePath)
    {
        try
        {
            var clientPath = Path.Join(_syncContext.RootDirectory, relativePath);
            return Path.Exists(clientPath) &&
                   !File.GetAttributes(clientPath).HasAnySyncFlag(SyncAttributes.OFFLINE);
        }
        catch (Exception ex)
        {
            logger.LogError("Error checking hydration state for {path}", relativePath, ex);
            return false;
        }
    }

    public void Dispose()
    {
        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                logger.LogDebug("Disposing SFTP watcher");
                _cancellationTokenSource.Cancel();
                
                // Safely disconnect the client
                try
                {
                    if (client?.IsConnected ?? false)
                    {
                        client.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Error disconnecting client during disposal", ex);
                }
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }
    }
}
