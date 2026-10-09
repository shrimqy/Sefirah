using System.Threading.Channels;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.Interop.Extensions;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
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
    private readonly IRemoteReadWriteService _remoteService;
    private readonly PlaceholdersService _placeholdersService;
    private readonly ILogger _logger;
    private readonly FileSystemWatcher _watcher;

    private string RootDirectory => _contextAccessor.Context.RootDirectory;

    public ClientWatcher(
        ISyncProviderContextAccessor contextAccessor,
        ChannelWriter<Func<Task>> taskWriter,
        FileLocker fileLocker,
        IRemoteReadWriteService remoteService,
        PlaceholdersService placeholdersService,
        ILogger logger
    )
    {
        _contextAccessor = contextAccessor;
        _taskWriter = taskWriter;
        _fileLocker = fileLocker;
        _remoteService = remoteService;
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
                        await SyncPendingChanges(relativePath);
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

        watcher.Error += (sender, e) => {
            var ex = e.GetException();
            _logger.Error("Client file watcher error", ex);
        };

        return watcher;
    }

    /// <summary>
    /// Uploads local changes under <paramref name="relativeDirectory"/> and removes in-sync items that are gone from the remote.
    /// </summary>
    public async Task SyncPendingChanges(string relativeDirectory)
    {
        var directoryInfo = new DirectoryInfo(Path.Join(RootDirectory, relativeDirectory));
        if (!directoryInfo.Exists || directoryInfo.Attributes.HasFlag(FileAttributes.Offline))
        {
            return;
        }

        if (relativeDirectory.Length > 0
            && CloudFilter.HasLocalChanges(directoryInfo.FullName)
            && !_remoteService.Exists(relativeDirectory))
        {
            await _remoteService.CreateDirectory(directoryInfo, relativeDirectory);
        }

        var remoteNames = _remoteService.EnumerateFiles(relativeDirectory).Select(x => x.Name)
            .Concat(_remoteService.EnumerateDirectories(relativeDirectory).Select(x => x.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // An empty listing from a dropped connection would look like everything was deleted
        var canRemove = _remoteService.IsConnected;

        foreach (var entry in directoryInfo.EnumerateFileSystemInfos().ToArray())
        {
            var relativePath = PathMapper.GetRelativePath(entry.FullName, RootDirectory);
            try
            {
                if (FileHelper.IsSystemFile(entry.FullName))
                {
                    continue;
                }

                var state = CloudFilter.GetPlaceholderState(entry.FullName);
                if (state is CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_INVALID)
                {
                    continue;
                }

                if (!CloudFilter.HasLocalChanges(state) && !remoteNames.Contains(entry.Name))
                {
                    if (canRemove)
                    {
                        using var locker = await _fileLocker.Lock(relativePath);
                        await _placeholdersService.Delete(relativePath);
                    }
                    continue;
                }

                if (entry is DirectoryInfo)
                {
                    await SyncPendingChanges(relativePath);
                }
                else if (CloudFilter.HasLocalChanges(state) && !entry.Attributes.HasFlag(FileAttributes.Offline))
                {
                    using var locker = await _fileLocker.Lock(relativePath);
                    await _placeholdersService.UpdateFile(relativePath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to sync pending change {relativePath}", ex);
            }
        }
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
