using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.Helpers;
using Sefirah.App.RemoteStorage.Interop;
using Sefirah.App.RemoteStorage.RemoteAbstractions;
using Sefirah.Common.Utils;
using System.Threading.Channels;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;

namespace Sefirah.App.RemoteStorage.Worker;
public sealed class SyncRootConnector(
    ISyncProviderContextAccessor contextAccessor,
    ChannelWriter<Func<Task>> taskWriter,
    FileLocker fileLocker,
    IRemoteReadWriteService remoteService,
    ILogger logger
)
{
    private readonly string _rootDirectory = contextAccessor.Context.RootDirectory;

    // Trying to prevent garbage collection of these callbacks
    private static CF_CALLBACK_REGISTRATION[] CallbackRegistrations;

    public CF_CONNECTION_KEY Connect()
    {
        logger.Debug("Connecting sync provider to {syncRootPath}", _rootDirectory);
        CallbackRegistrations = CloudFilter.ConnectSyncRoot(
            _rootDirectory,
            new SyncRootEvents
            {
                FetchPlaceholders = FetchPlaceholders,
                FetchData = (in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters) =>
                    FetchData(callbackInfo, callbackParameters),
                OnCloseCompletion = OnCloseCompletion,
                OnRenameCompletion = (in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters) =>
                {
                    var volumeDosName = callbackInfo.VolumeDosName;
                    var oldPath = callbackParameters.RenameCompletion.SourcePath;
                    var newPath = callbackInfo.NormalizedPath;
                    taskWriter.TryWrite(() => OnRenameCompletion(volumeDosName, oldPath, newPath));
                },
                OnDeleteCompletion = (in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters) =>
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
        logger.Debug("Disconnecting sync provider, {connectionKey}", connectionKey);
        CloudFilter.DisconnectSyncRoot(connectionKey);
    }

    private void FetchPlaceholders(in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters)
    {
        logger.Debug("Fetch Placeholders '{path}' '{pattern}' Flags: {flags}", 
            callbackInfo.NormalizedPath, 
            callbackParameters.FetchPlaceholders.Pattern,
            callbackParameters.FetchPlaceholders.Flags);

        var clientDirectory = Path.Join(callbackInfo.VolumeDosName, callbackInfo.NormalizedPath[1..]);
        var relativeDirectory = PathMapper.GetRelativePath(clientDirectory, _rootDirectory);

        try
        {
            var fileInfos = remoteService.EnumerateFiles(relativeDirectory, callbackParameters.FetchPlaceholders.Pattern)
                .Where(x => !FileHelper.IsSystemFile(x.RelativePath));
            var directoryInfos = remoteService.EnumerateDirectories(relativeDirectory, callbackParameters.FetchPlaceholders.Pattern);
            var fileSystemInfos = fileInfos.Concat<RemoteFileSystemInfo>(directoryInfos).ToArray();

            CloudFilter.TransferPlaceholders(callbackInfo, fileSystemInfos);
        }
        catch (Exception ex)
        {
            logger.Error("Error transferring placeholders", ex);
        }
    }

    private async void FetchData(CF_CALLBACK_INFO callbackInfo, CF_CALLBACK_PARAMETERS callbackParameters)
    {
        logger.Debug(
            "Fetch data, {file}, fileSize: {fileSize}, offset: {offset}, total: {total}",
            callbackInfo.NormalizedPath,
            callbackInfo.FileSize,
            callbackParameters.FetchData.RequiredFileOffset,
            callbackParameters.FetchData.RequiredLength
        );
        try
        {
            var clientFile = Path.Join(callbackInfo.VolumeDosName, callbackInfo.NormalizedPath[1..]);

            var bufferSize = Math.Min(callbackParameters.FetchData.RequiredLength, 4096 * 4);
            var buffer = new byte[bufferSize];
            long startOffset = callbackParameters.FetchData.RequiredFileOffset;
            long currentOffset = startOffset;
            long targetOffset = callbackParameters.FetchData.RequiredFileOffset
                + callbackParameters.FetchData.RequiredLength;
            long readLength = 0;

            var relativeFile = PathMapper.GetRelativePath(clientFile, _rootDirectory);
            using var fileStream = await remoteService.GetFileStream(relativeFile);
            fileStream.Seek(currentOffset, SeekOrigin.Begin);
            while (currentOffset <= targetOffset && (readLength = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                // Update the transfer progress
                CloudFilter.ReportProgress(callbackInfo, callbackInfo.FileSize, currentOffset + readLength);
                // TODO: Tell the Shell so File Explorer can display the progress bar in its view

                // This helper function tells the Cloud File API about the transfer,
                // which will copy the data to the local syncroot
                CloudFilter.TransferData(callbackInfo, buffer, currentOffset, readLength);

                currentOffset += readLength;
            }
        }
        catch (Exception ex)
        {
            logger.Error("Failed to transfer server->client", ex);

            CloudFilter.TransferData(
                callbackInfo,
                null,
                callbackParameters.FetchData.RequiredFileOffset,
                callbackParameters.FetchData.RequiredLength,
                success: false
            );
        }
    }

    private void OnCloseCompletion(in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters)
    {
        logger.Debug("SyncRoot CloseCompletion {path} {flags}", callbackInfo.NormalizedPath, callbackParameters.CloseCompletion.Flags);
    }

    private async Task OnRenameCompletion(string volumeDosName, string oldPath, string newPath)
    {
        logger.Debug("SyncRoot Rename {old} -> {new}", oldPath, newPath);
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
            logger.Error("Rename server object failed", ex);
        }
    }

    private async Task OnDeleteCompletion(string volumeDosName, string path)
    {
        logger.Debug("SyncRoot Delete {path}", path);
        var clientPath = Path.Join(volumeDosName, path[1..]);
        // For files created in client, sometimes it's not actually deleted yet. Wait until it's really gone.
        for (var attempt = 0; attempt < 60 && Path.Exists(clientPath); attempt++)
        {
            logger.Debug("File has not yet been deleted, waiting before retry");
            await Task.Delay(500);
        }
        if (Path.Exists(clientPath))
        {
            logger.Warn("Received delete completion, but file has not been deleted: {clientPath}", clientPath);
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
            logger.Error("Delete server object failed", ex);
        }
    }
}
