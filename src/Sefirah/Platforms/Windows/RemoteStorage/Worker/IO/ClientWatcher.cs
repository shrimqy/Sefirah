using System.Threading.Channels;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.Interop.Extensions;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;
using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker.IO;

/// <summary>Watches local sync root and updates remote storage.</summary>
public partial class ClientWatcher : IDisposable
{
    private readonly ISyncProviderContextAccessor _contextAccessor;
    private readonly ChannelWriter<Func<Task>> _taskWriter;
    private readonly FileLocker _fileLocker;
    private readonly PlaceholdersService _placeholdersService;
    private readonly ILogger _logger;
    private readonly FileSystemWatcher _watcher;

    private string RootDirectory => _contextAccessor.Context.RootDirectory;

    public ClientWatcher(
        ISyncProviderContextAccessor contextAccessor,
        ChannelWriter<Func<Task>> taskWriter,
        FileLocker fileLocker,
        PlaceholdersService placeholdersService,
        ILogger logger
    )
    {
        _contextAccessor = contextAccessor;
        _taskWriter = taskWriter;
        _fileLocker = fileLocker;
        _placeholdersService = placeholdersService;
        _logger = logger;
        _watcher = CreateWatcher();
    }

    private FileSystemWatcher CreateWatcher()
    {
        var watcher = new FileSystemWatcher(RootDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.Attributes
                | NotifyFilters.LastWrite,
            InternalBufferSize = 64 * 1024,
        };

        watcher.Changed += async (sender, e) => {
            try
            {
                if (e.ChangeType is not WatcherChangeTypes.Changed || !Path.Exists(e.FullPath) || FileHelper.IsSystemFile(e.FullPath))
                {
                    return;
                }

                var fileInfo = new FileInfo(e.FullPath);

                await _taskWriter.WriteAsync(async () => {
                    CF_PLACEHOLDER_STATE state;
                    try
                    {
                        state = CloudFilter.GetPlaceholderState(e.FullPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Unable to get placeholder state for {e.FullPath}", ex);
                        return;
                    }

                    // A fully-present (not partial), unpinned file is a "Free up space" request.
                    var isDehydrationRequested = fileInfo.Attributes.IsDehydrationRequested()
                        && !fileInfo.Attributes.HasFlag(FileAttributes.Directory)
                        && !state.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PARTIALLY_ON_DISK);

                    // A pinned folder, or a pinned online-only file, is an "Always keep on this device" request.
                    var isHydrationRequested = fileInfo.Attributes.IsPinned() 
                        && (fileInfo.Attributes.HasFlag(FileAttributes.Directory) 
                        || fileInfo.Attributes.HasFlag(FileAttributes.Offline));

                    // Skip already-synced files (hydration also raises Changed), but let dehydrate and pin requests through.
                    if (!CloudFilter.HasLocalChanges(state) && !isDehydrationRequested && !isHydrationRequested)
                    {
                        return;
                    }

                    var relativePath = PathMapper.GetRelativePath(e.FullPath, RootDirectory);
                    using var locker = await _fileLocker.Lock(relativePath);

                    if (isHydrationRequested)
                    {
                        try
                        {
                            _placeholdersService.Hydrate(relativePath);
                        }
                        catch (HFileException)
                        {
                            _logger.Warn($"Unable to hydrate placeholder for {e.FullPath} - connection may be lost");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Hydrate failed: {e.FullPath}", ex);
                        }
                    }
                    else if (isDehydrationRequested)
                    {
                        try
                        {
                            CloudFilter.DehydratePlaceholder(e.FullPath, relativePath, fileInfo.Length);
                        }
                        catch (HFileException)
                        {
                            _logger.Warn($"Unable to dehydrate placeholder for {e.FullPath} - connection may be lost");
                        }
                        return;
                    }

                    if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        //var directoryInfo = new DirectoryInfo(e.FullPath);
                        //await _serverService.UpdateDirectory(directoryInfo, relativePath);
                    }
                    else
                    {
                        await _placeholdersService.UpdateFile(relativePath);
                    }

                });
            }
            catch (Exception ex)
            {
                _logger.Error($"Unhandled error in file system watcher for {e.FullPath}", ex);
            }
        };

        watcher.Created += async (sender, e) => {

            await _taskWriter.WriteAsync(async () => {
                try
                {
                    if (!CloudFilter.HasLocalChanges(e.FullPath))
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Unable to get placeholder state for {e.FullPath}", ex);
                    return;
                }

                var relativePath = PathMapper.GetRelativePath(e.FullPath, RootDirectory);
                try
                {
                    if (File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory))
                    {
                        await _placeholdersService.SyncPendingChanges(relativePath);
                    }
                    else
                    {
                        using var locker = await _fileLocker.Lock(relativePath);
                        await _placeholdersService.UpdateFile(relativePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Create failed: {e.FullPath}", ex);
                }
            });
        };

        // Placeholders were already renamed on the remote in SyncRootConnector.OnRename; Windows clears their in-sync state
        watcher.Renamed += async (sender, e) => {
            await _taskWriter.WriteAsync(async () => {
                if (FileHelper.IsSystemFile(e.FullPath))
                {
                    return;
                }

                var relativePath = PathMapper.GetRelativePath(e.FullPath, RootDirectory);
                try
                {
                    // Null for new items, which get their identity when converted below
                    if (CloudFilter.GetPlaceholderIdentity(e.FullPath) is not null)
                    {
                        CloudFilter.SetPlaceholderIdentity(e.FullPath, relativePath);
                    }

                    var attributes = File.GetAttributes(e.FullPath);
                    // Online-only: no local content to differ from the remote
                    if (attributes.HasFlag(FileAttributes.Offline))
                    {
                        CloudFilter.SetInSyncState(e.FullPath);
                        // Explorer keeps showing the old status icon otherwise
                        ShellNotify.NotifyUpdate(e.FullPath);
                    }
                    // Created on the remote if it isn't there yet, then marked in sync
                    else if (attributes.HasFlag(FileAttributes.Directory))
                    {
                        await _placeholdersService.SyncPendingChanges(relativePath);
                    }
                    // Uploaded if it differs from or is missing on the remote, then marked in sync
                    else
                    {
                        using var locker = await _fileLocker.Lock(relativePath);
                        await _placeholdersService.UpdateFile(relativePath);
                    }
                }
                catch (Exception) when (!Path.Exists(e.FullPath))
                {
                    // Gone already, e.g. renamed again
                }
                catch (Exception ex)
                {
                    _logger.Error($"Rename failed: {e.FullPath}", ex);
                }
            });
        };

        watcher.Error += (sender, e) => {
            var ex = e.GetException();
            _logger.Error("Client file watcher error", ex);
        };

        return watcher;
    }

    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
