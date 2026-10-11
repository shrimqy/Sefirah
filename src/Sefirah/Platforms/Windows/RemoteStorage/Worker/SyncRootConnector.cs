using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.Interop.SyncRoot;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;

public sealed class SyncRootConnector(
    ISyncProviderContextAccessor contextAccessor,
    FileLocker fileLocker,
    IRemoteReadWriteService remoteService,
    ILogger logger
)
{
    private readonly string _rootDirectory = contextAccessor.Context.RootDirectory;

    private readonly ConcurrentDictionary<CF_TRANSFER_KEY, CancellationTokenSource> _cancellationTokenSources = new();
    private readonly ConcurrentDictionary<CF_TRANSFER_KEY, Task> _transferTasks = new();
    private static readonly TimeSpan TransferKeyReuseWaitTimeout = TimeSpan.FromSeconds(5);

    // Transfer chunk size. The Cloud File API rejects any TRANSFER_DATA whose length isn't 4 KiB-aligned (unless it ends at EOF)
    private const int ChunkSize = 4096 * 4;

    // Trying to prevent garbage collection of these callbacks
    private CF_CALLBACK_REGISTRATION[]? CallbackRegistrations;

    public CF_CONNECTION_KEY Connect()
    {
        logger.Debug($"Connecting sync provider to {_rootDirectory}");
        CallbackRegistrations = CloudFilter.ConnectSyncRoot(
            _rootDirectory,
            new SyncRootEvents
            {
                FetchPlaceholders = FetchPlaceholders,
                FetchData = (in callbackInfo, in callbackParameters) => FetchData(callbackInfo, callbackParameters),
                CancelFetchData = CancelFetchData,
                OnCloseCompletion = OnCloseCompletion,
                OnRename = (in callbackInfo, in callbackParameters) =>
                {
                    var info = callbackInfo;
                    var isOwnProcess = IsOwnProcess(callbackInfo);
                    var sourcePath = ToClientPath(callbackInfo, callbackInfo.NormalizedPath);
                    var targetPath = ToClientPath(callbackInfo, callbackParameters.Rename.TargetPath);
                    var isDirectory = callbackParameters.Rename.Flags.HasFlag(CF_CALLBACK_RENAME_FLAGS.CF_CALLBACK_RENAME_FLAG_IS_DIRECTORY);
                    _ = Task.Run(() => OnRename(info, isOwnProcess, sourcePath, targetPath, isDirectory));
                },                OnDelete = (in callbackInfo, in callbackParameters) =>
                {
                    var info = callbackInfo;
                    var isOwnProcess = IsOwnProcess(callbackInfo);
                    var path = ToClientPath(callbackInfo, callbackInfo.NormalizedPath);
                    var flags = callbackParameters.Delete.Flags;
                    _ = Task.Run(() => OnDelete(info, isOwnProcess, path, flags));
                },
            },
            out var connectionKey
        );

        return connectionKey;
    }

    public void Disconnect(CF_CONNECTION_KEY connectionKey)
    {
        CloudFilter.DisconnectSyncRoot(connectionKey);
    }

    private void FetchPlaceholders(in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters)
    {
        logger.Debug($"Fetch Placeholders '{callbackInfo.NormalizedPath}' '{callbackParameters.FetchPlaceholders.Pattern}' Flags: {callbackParameters.FetchPlaceholders.Flags}");

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
            logger.Error("Error transferring placeholders", ex);
        }
    }

    private void FetchData(CF_CALLBACK_INFO callbackInfo, CF_CALLBACK_PARAMETERS callbackParameters)
    {
        logger.Debug($"Fetch data, {callbackInfo.NormalizedPath}, fileSize: {callbackInfo.FileSize}, offset: {callbackParameters.FetchData.RequiredFileOffset}, total: {callbackParameters.FetchData.RequiredLength}");

        var transferKey = callbackInfo.TransferKey;

        if (_transferTasks.TryGetValue(transferKey, out var existingTask))
        {
            try 
            { 
                existingTask.Wait(TransferKeyReuseWaitTimeout); 
            }
            catch (Exception) 
            { 
                // prior transfer faulted or cancelled, proceed
            }
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

            var bufferSize = (int)Math.Min(callbackParameters.FetchData.RequiredLength, ChunkSize);
            var buffer = new byte[bufferSize];
            long currentOffset = callbackParameters.FetchData.RequiredFileOffset;
            long targetOffset = callbackParameters.FetchData.RequiredFileOffset + callbackParameters.FetchData.RequiredLength;

            var relativeFile = PathMapper.GetRelativePath(clientFile, _rootDirectory);
            using var fileStream = await remoteService.GetFileStream(relativeFile);
            fileStream.Seek(currentOffset, SeekOrigin.Begin);

            // A bare Stream.Read over SFTP can return a short, unaligned count mid-file (which fails the transfer
            // with error 380), so ReadAtLeast fills each chunk completely, and we never read past the requested range.
            while (currentOffset < targetOffset)
            {
                var bytesToRead = (int)Math.Min(buffer.Length, targetOffset - currentOffset);
                var readLength = fileStream.ReadAtLeast(buffer.AsSpan(0, bytesToRead), bytesToRead, throwOnEndOfStream: false);
                if (readLength == 0)
                {
                    break;
                }

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
            logger.Debug($"Fetch data cancelled for {callbackInfo.NormalizedPath}");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to transfer server->client", ex);
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
                logger.Error("Failed to signal transfer failure to Cloud Filter", innerEx);
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

    /// <summary>Renames the item on the remote before Windows renames it locally; the local rename fails if this fails.</summary>
    private async Task OnRename(CF_CALLBACK_INFO callbackInfo, bool isOwnProcess, string sourcePath, string targetPath, bool isDirectory)
    {
        var success = true;
        // Our own renames, e.g. recycling an item deleted on the remote, are already reflected there
        if (!isOwnProcess && IsInRoot(sourcePath))
        {
            try
            {
                var oldRelativePath = PathMapper.GetRelativePath(sourcePath, _rootDirectory);
                using var oldLocker = await fileLocker.Lock(oldRelativePath);
                if (remoteService.Exists(oldRelativePath))
                {
                    // Moved out of the sync root, including to the Recycle Bin
                    if (!IsInRoot(targetPath))
                    {
                        DeleteOnRemote(oldRelativePath, isDirectory);
                    }
                    else
                    {
                        var newRelativePath = PathMapper.GetRelativePath(targetPath, _rootDirectory);
                        using var newLocker = await fileLocker.Lock(newRelativePath);
                        if (isDirectory)
                        {
                            remoteService.MoveDirectory(oldRelativePath, newRelativePath);
                        }
                        else
                        {
                            remoteService.MoveFile(oldRelativePath, newRelativePath);
                        }
                        logger.Info($"Renamed {oldRelativePath} to {newRelativePath} on the remote");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Rename on the remote failed: {sourcePath} -> {targetPath}", ex);
                success = false;
            }
        }

        try
        {
            CloudFilter.AckRename(callbackInfo, success);
        }
        catch (Exception ex)
        {
            logger.Error($"Ack rename failed for {sourcePath}", ex);
        }
    }

    /// <summary>Deletes the item on the remote before Windows deletes it locally; the local delete fails if this fails.</summary>
    private async Task OnDelete(CF_CALLBACK_INFO callbackInfo, bool isOwnProcess, string path, CF_CALLBACK_DELETE_FLAGS flags)
    {
        var success = true;
        // CF_CALLBACK_DELETE_FLAG_IS_UNDELETE: the item is being restored, so nothing to delete on the remote
        var isUndelete = ((uint)flags & 0x2) != 0;
        if (!isOwnProcess && !isUndelete)
        {
            try
            {
                var relativePath = PathMapper.GetRelativePath(path, _rootDirectory);
                using var locker = await fileLocker.Lock(relativePath);
                if (remoteService.Exists(relativePath))
                {
                    DeleteOnRemote(relativePath, flags.HasFlag(CF_CALLBACK_DELETE_FLAGS.CF_CALLBACK_DELETE_FLAG_IS_DIRECTORY));
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Delete on the remote failed: {path}", ex);
                success = false;
            }
        }

        try
        {
            CloudFilter.AckDelete(callbackInfo, success);
        }
        catch (Exception ex)
        {
            logger.Error($"Ack delete failed for {path}", ex);
        }
    }

    private void DeleteOnRemote(string relativePath, bool isDirectory)
    {
        if (isDirectory)
        {
            remoteService.DeleteDirectory(relativePath);
        }
        else
        {
            remoteService.DeleteFile(relativePath);
        }
        logger.Info($"Deleted {relativePath} on the remote");
    }

    private bool IsInRoot(string path) =>
        path.StartsWith(Path.TrimEndingDirectorySeparator(_rootDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string ToClientPath(in CF_CALLBACK_INFO callbackInfo, string normalizedPath) =>
        Path.Join(callbackInfo.VolumeDosName, normalizedPath[1..]);

    /// <summary>Whether the operation was started by this process. Read during the callback only.</summary>
    private static bool IsOwnProcess(in CF_CALLBACK_INFO callbackInfo)
    {
        // CF_PROCESS_INFO: ProcessId at 4
        var processInfo = (IntPtr)callbackInfo.ProcessInfo;
        return processInfo != IntPtr.Zero && Marshal.ReadInt32(processInfo, 4) == Environment.ProcessId;
    }
}
