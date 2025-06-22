using System.Threading.Channels;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using Sefirah.Platforms.Windows.RemoteStorage.Worker;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker.IO;
public sealed class RemoteWatcher(
    IRemoteReadService remoteReadService,
    IRemoteWatcher remoteWatcher,
    ChannelWriter<Func<Task>> taskWriter,
    FileLocker fileLocker,
    PlaceholdersService placeholderService,
    ILogger logger
) : IDisposable
{
    public void Start(CancellationToken stoppingToken)
    {
        remoteWatcher.Created += HandleCreated;
        remoteWatcher.Changed += HandleChanged;
        remoteWatcher.Renamed += HandleRenamed;
        remoteWatcher.Deleted += HandleDeleted;
        remoteWatcher.Start(stoppingToken);
    }

    private async Task HandleCreated(string relativePath)
    {
        relativePath = PathMapper.NormalizePath(relativePath);
        await taskWriter.WriteAsync(async () =>
        {
            if (FileHelper.IsSystemDirectory(relativePath)) return;
        
            using var locker = await fileLocker.Lock(relativePath);
            try
            {
                if (remoteReadService.IsDirectory(relativePath))
                {
                    await placeholderService.CreateOrUpdateDirectory(relativePath);
                }
                else
                {
                    await placeholderService.CreateOrUpdateFile(relativePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handle Created failed for: {relativePath}",relativePath);
            }
        });
    }

    private async Task HandleChanged(string relativePath)
    {
        relativePath = PathMapper.NormalizePath(relativePath);

        await taskWriter.WriteAsync(async () =>
        {
            using var locker = await fileLocker.Lock(relativePath);
            try
            {
                if (remoteReadService.IsDirectory(relativePath))
                {
                    await placeholderService.UpdateDirectory(relativePath);
                    
                    var files = remoteReadService.EnumerateFiles(relativePath);
                    foreach (var file in files)
                    {
                        try
                        {
                            await placeholderService.UpdateFile(file.RelativePath);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to update file {file}", file.RelativePath);
                        }
                    }
                }
                else
                {
                    await placeholderService.UpdateFile(relativePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handle Changed failed");
            }
        });
    }

    private async Task HandleRenamed(string oldRelativePath, string newRelativePath)
    {
        // Brief pause to let client rename finish before reflecting it back
        await Task.Delay(1000);
        oldRelativePath = PathMapper.NormalizePath(oldRelativePath);
        newRelativePath = PathMapper.NormalizePath(newRelativePath);

        await taskWriter.WriteAsync(async () =>
        {
            using var oldLocker = await fileLocker.Lock(oldRelativePath);
            using var newLocker = await fileLocker.Lock(newRelativePath);
            try
            {
                if (remoteReadService.IsDirectory(newRelativePath))
                {
                    await placeholderService.RenameDirectory(oldRelativePath, newRelativePath);
                }
                else
                {
                    await placeholderService.RenameFile(oldRelativePath, newRelativePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Rename placeholder failed");
            }
        });
    }

    private async Task HandleDeleted(string relativePath)
    {
        // Brief pause to let client finish before reflecting it back
        await Task.Delay(1000);
        relativePath = PathMapper.NormalizePath(relativePath);
        logger.LogDebug("Deleted {path}", relativePath);
        await taskWriter.WriteAsync(async () =>
        {
            using var locker = await fileLocker.Lock(relativePath);
            try
            {
                placeholderService.Delete(relativePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Delete placeholder failed");
            }
        });
    }

    public void Dispose()
    {
        remoteWatcher.Created -= HandleCreated;
        remoteWatcher.Changed -= HandleChanged;
        remoteWatcher.Renamed -= HandleRenamed;
        remoteWatcher.Deleted -= HandleDeleted;
    }
}
