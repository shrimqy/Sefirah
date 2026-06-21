using System.Threading.Channels;
using Renci.SshNet.Common;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker.IO;

/// <summary>Updates local placeholders for events raised from <see cref="IRemoteWatcher"/>.</summary>
public sealed partial class RemoteWatcher(
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
                if (!remoteReadService.Exists(relativePath))
                {
                    // path no longer exists on the server
                    return;
                }

                if (remoteReadService.IsDirectory(relativePath))
                {
                    await placeholderService.CreateOrUpdateDirectory(relativePath);
                }
                else
                {
                    await placeholderService.CreateOrUpdateFile(relativePath);
                }
            }
            catch (SshConnectionException) { }
            catch (Exception ex)
            {
                logger.Error($"Handle Created failed for: {relativePath}", ex);
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
                            logger.Error($"Failed to update file {file.RelativePath}", ex);
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
                logger.Error("Handle Changed failed", ex);
            }
        });
    }

    private async Task HandleRenamed(string oldRelativePath, string newRelativePath)
    {
        // Brief pause to let client rename finish before reflecting it back
        //await Task.Delay(1000);
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
                logger.Error("Rename placeholder failed", ex);
            }
        });
    }

    private async Task HandleDeleted(string relativePath)
    {
        // Brief pause to let client finish before reflecting it back
        //await Task.Delay(1000);
        relativePath = PathMapper.NormalizePath(relativePath);
        logger.Debug($"Deleted {relativePath}");
        await taskWriter.WriteAsync(async () =>
        {
            using var locker = await fileLocker.Lock(relativePath);
            try
            {
                placeholderService.Delete(relativePath);
            }
            catch (Exception ex)
            {
                logger.Error("Delete placeholder failed", ex);
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
