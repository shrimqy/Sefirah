using System.Collections.Concurrent;
using System.Threading.Channels;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.Interop.SyncRoot;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;
using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;
public sealed class SyncRootConnector(
    ISyncProviderContextAccessor contextAccessor,
    ChannelWriter<Func<Task>> taskWriter,
    FileLocker fileLocker,
    IRemoteReadWriteService remoteService,
    ILogger logger
)
{
    private readonly string _rootDirectory = contextAccessor.Context.RootDirectory;

    private readonly ConcurrentDictionary<CF_TRANSFER_KEY, CancellationTokenSource> _cancellationTokenSources = new();
    private readonly ConcurrentDictionary<CF_TRANSFER_KEY, Task> _transferTasks = new();
    private static readonly TimeSpan TransferKeyReuseWaitTimeout = TimeSpan.FromSeconds(5);

    // Trying to prevent garbage collection of these callbacks
    private CF_CALLBACK_REGISTRATION[]? CallbackRegistrations;

    public CF_CONNECTION_KEY Connect()
    {
        logger.LogDebug("Connecting sync provider to {syncRootPath}", _rootDirectory);
        CallbackRegistrations = CloudFilter.ConnectSyncRoot(
            _rootDirectory,
            new SyncRootEvents
            {
                FetchPlaceholders = FetchPlaceholders,
                FetchData = (in callbackInfo, in callbackParameters) => FetchData(callbackInfo, callbackParameters),
                CancelFetchData = CancelFetchData,
                OnCloseCompletion = OnCloseCompletion,
                OnRenameCompletion = (in callbackInfo, in callbackParameters) =>
                {
                    var volumeDosName = callbackInfo.VolumeDosName;
                    var oldPath = callbackParameters.RenameCompletion.SourcePath;
                    var newPath = callbackInfo.NormalizedPath;
                    taskWriter.TryWrite(() => OnRenameCompletion(volumeDosName, oldPath, newPath));
                },
                OnDeleteCompletion = (in callbackInfo, in callbackParameters) =>
                {
                    var volumeDosName = callbackInfo.VolumeDosName;
                    var path = callbackInfo.NormalizedPath;
                    taskWriter.TryWrite(() => OnDeleteCompletion(volumeDosName, path));
                },
            },
            out var connectionKey
        );

        return connectionKey;
    }

    public void Disconnect(CF_CONNECTION_KEY connectionKey)
    {
        logger.LogDebug("Disconnecting sync provider, {connectionKey}", connectionKey);
        CloudFilter.DisconnectSyncRoot(connectionKey);
    }

    private void FetchPlaceholders(in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters)
    {
        logger.LogDebug("Fetch Placeholders '{path}' '{pattern}' Flags: {flags}", 
            callbackInfo.NormalizedPath, 
            callbackParameters.FetchPlaceholders.Pattern,
            callbackParameters.FetchPlaceholders.Flags);

        var clientDirectory = Path.Join(callbackInfo.VolumeDosName, callbackInfo.NormalizedPath[1..]);
        var relativeDirectory = PathMapper.GetRelativePath(clientDirectory, _rootDirectory);
        try
        {
            var fileInfos = remoteService.EnumerateFiles(relativeDirectory, callbackParameters.FetchPlaceholders.Pattern);
            var directoryInfos = remoteService.EnumerateDirectories(relativeDirectory, callbackParameters.FetchPlaceholders.Pattern)
                .Where(dir => !FileHelper.IsSystemDirectory(dir.RelativePath));
            var fileSystemInfos = fileInfos.Concat<RemoteFileSystemInfo>(directoryInfos).ToArray();

            CloudFilter.TransferPlaceholders(callbackInfo, fileSystemInfos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transferring placeholders");
        }
    }
    
    // Right now it just deletes the placeholders which was deleted in remote
    public void UpdatePlaceholders(string clientDirectory)
    {
        if (!Directory.Exists(clientDirectory))
            return;

        foreach (var clientFile in Directory.GetFiles(clientDirectory))
        {
            var relativePath = PathMapper.GetRelativePath(clientFile, _rootDirectory);
            try
            {
                if (!remoteService.Exists(relativePath))
                {
                    File.Delete(clientFile);
                }
            }
            catch (Exception) { }
        }

        // Check and delete directories recursively
        foreach (var clientDir in Directory.GetDirectories(clientDirectory))
        {
            var relativePath = PathMapper.GetRelativePath(clientDir, _rootDirectory);
            try
            {
                if (!remoteService.Exists(relativePath))
                {
                    Directory.Delete(clientDir, recursive: true);
                }
                else
                {
                    //  recursively check hydrated directories
                    var attributes = File.GetAttributes(clientDir);
                    bool isHydrated = !attributes.HasFlag(FileAttributes.Offline);
                
                    if (isHydrated)
                    {
                        // Directory exists remotely and is hydrated, check its contents recursively
                        UpdatePlaceholders(clientDir);
                    }
                }
            }
            catch (Exception) { }
        }
    }

    private void FetchData(CF_CALLBACK_INFO callbackInfo, CF_CALLBACK_PARAMETERS callbackParameters)
    {
        logger.LogDebug(
            "Fetch data, {file}, fileSize: {fileSize}, offset: {offset}, total: {total}",
            callbackInfo.NormalizedPath,
            callbackInfo.FileSize,
            callbackParameters.FetchData.RequiredFileOffset,
            callbackParameters.FetchData.RequiredLength
        );

        var transferKey = callbackInfo.TransferKey;

        if (_transferTasks.TryGetValue(transferKey, out var existingTask))
        {
            existingTask.Wait(TransferKeyReuseWaitTimeout);
        }

        var cts = new CancellationTokenSource();
        _cancellationTokenSources[transferKey] = cts;

        var wrappedTask = new Task<Task>(() => FetchDataAsync(callbackInfo, callbackParameters, cts.Token));
        _transferTasks[transferKey] = wrappedTask.Unwrap();
        wrappedTask.RunSynchronously();
    }

    private async Task FetchDataAsync(CF_CALLBACK_INFO callbackInfo, CF_CALLBACK_PARAMETERS callbackParameters, CancellationToken cancellationToken)
    {
        try
        {
            var clientFile = Path.Join(callbackInfo.VolumeDosName, callbackInfo.NormalizedPath[1..]);

            var bufferSize = Math.Min(callbackParameters.FetchData.RequiredLength, 4096 * 4);
            var buffer = new byte[bufferSize];
            long currentOffset = callbackParameters.FetchData.RequiredFileOffset;
            long targetOffset = callbackParameters.FetchData.RequiredFileOffset
                + callbackParameters.FetchData.RequiredLength;
            long readLength = 0;

            var relativeFile = PathMapper.GetRelativePath(clientFile, _rootDirectory);
            using var fileStream = await remoteService.GetFileStream(relativeFile);
            fileStream.Seek(currentOffset, SeekOrigin.Begin);
            while (currentOffset <= targetOffset && (readLength = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Update the transfer progress
                CloudFilter.ReportProgress(callbackInfo, callbackInfo.FileSize, currentOffset + readLength);
                // TODO: Tell the Shell so File Explorer can display the progress bar in its view

                // This helper function tells the Cloud File API about the transfer,
                // which will copy the data to the local syncroot
                CloudFilter.TransferData(callbackInfo, buffer, currentOffset, readLength);

                currentOffset += readLength;
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Fetch data cancelled for {file}", callbackInfo.NormalizedPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to transfer server->client");
            try
            {
                CloudFilter.TransferData(
                    callbackInfo,
                    null,
                    callbackParameters.FetchData.RequiredFileOffset,
                    callbackParameters.FetchData.RequiredLength,
                    success: false
                );
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Failed to signal transfer failure to Cloud Filter");
            }
        }
        finally
        {
            _cancellationTokenSources.TryRemove(callbackInfo.TransferKey, out var cts);
            cts?.Dispose();
            _transferTasks.TryRemove(callbackInfo.TransferKey, out _);
        }
    }

    private void CancelFetchData(in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS _)
    {
        if (_cancellationTokenSources.TryRemove(callbackInfo.TransferKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void OnCloseCompletion(in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters)
    {
        //logger.Debug("SyncRoot CloseCompletion {path} {flags}", callbackInfo.NormalizedPath, callbackParameters.CloseCompletion.Flags);
    }

    private async Task OnRenameCompletion(string volumeDosName, string oldPath, string newPath)
    {
        logger.LogDebug("SyncRoot Rename {old} -> {new}", oldPath, newPath);
        var oldClientPath = Path.Join(volumeDosName, oldPath[1..]);
        var oldRelativePath = PathMapper.GetRelativePath(oldClientPath, _rootDirectory);
        var newClientPath = Path.Join(volumeDosName, newPath[1..]);
        var newRelativePath = PathMapper.GetRelativePath(newClientPath, _rootDirectory);
        using var oldLocker = await fileLocker.Lock(oldRelativePath);
        using var newLocker = await fileLocker.Lock(newRelativePath);
        try
        {
            if (!remoteService.Exists(oldRelativePath))
            {
                return;
            }
            // If moving outside of sync directory, treat like a delete
            if (!newClientPath.StartsWith(_rootDirectory))
            {
                if (remoteService.IsDirectory(oldRelativePath))
                {
                    remoteService.DeleteDirectory(oldRelativePath);
                }
                else
                {
                    remoteService.DeleteFile(oldRelativePath);
                }
                return;
            }
            if (File.GetAttributes(newClientPath).HasFlag(FileAttributes.Directory))
            {
                remoteService.MoveDirectory(oldRelativePath, newRelativePath);
            }
            else
            {
                remoteService.MoveFile(oldRelativePath, newRelativePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rename server object failed");
        }
    }

    private async Task OnDeleteCompletion(string volumeDosName, string path)
    {
        logger.LogDebug("SyncRoot Delete {path}", path);
        var clientPath = Path.Join(volumeDosName, path[1..]);
        // For files created in client, sometimes it's not actually deleted yet. Wait until it's really gone.
        for (var attempt = 0; attempt < 60 && Path.Exists(clientPath); attempt++)
        {
            logger.LogDebug("File has not yet been deleted, waiting before retry");
            await Task.Delay(500);
        }
        if (Path.Exists(clientPath))
        {
            logger.LogWarning("Received delete completion, but file has not been deleted: {clientPath}", clientPath);
            return;
        }
        var relativePath = PathMapper.GetRelativePath(clientPath, _rootDirectory);
        using var locker = await fileLocker.Lock(relativePath);
        if (!remoteService.Exists(relativePath))
        {
            return;
        }
        try
        {
            if (remoteService.IsDirectory(relativePath))
            {
                remoteService.DeleteDirectory(relativePath);
            }
            else
            {
                remoteService.DeleteFile(relativePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Delete server object failed");
        }
    }
}
