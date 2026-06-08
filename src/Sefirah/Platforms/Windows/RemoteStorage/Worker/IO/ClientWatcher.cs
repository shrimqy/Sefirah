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
                    try
                    {
                        var state = CloudFilter.GetPlaceholderState(e.FullPath);

                        // FileSystemWatcher also fires on hydration updates, so skip if already in-sync.
                        if (state.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC))
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
                    using var locker = await _fileLocker.Lock(relativePath);

                    if (fileInfo.Attributes.HasAllSyncFlags(SyncAttributes.PINNED | (int)FileAttributes.Offline))
                    {
                        if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                        {
                            try
                            {
                                _placeholdersService.CreateBulk(relativePath);
                                var childItems = Directory.EnumerateFiles(e.FullPath, "*", SearchOption.AllDirectories)
                                    .Where((x) => !FileHelper.IsSystemFile(x))
                                    .ToArray();
                                foreach (var childItem in childItems)
                                {
                                    try
                                    {
                                        CloudFilter.HydratePlaceholder(childItem);
                                    }
                                    catch (HFileException)
                                    {
                                        _logger.Warn($"Unable to hydrate placeholder for {childItem} - connection may be lost");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.Error($"Hydrate file failed: {childItem}", ex);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Error processing directory {e.FullPath}", ex);
                            }
                        }
                        else
                        {
                            try
                            {
                                CloudFilter.HydratePlaceholder(e.FullPath);
                            }
                            catch (HFileException)
                            {
                                _logger.Warn($"Unable to hydrate placeholder for {e.FullPath} - connection may be lost");
                            }
                        }
                    }
                    else if (
                        fileInfo.Attributes.HasAnySyncFlag(SyncAttributes.UNPINNED)
                        && !fileInfo.Attributes.HasFlag(FileAttributes.Offline)
                        && !fileInfo.Attributes.HasFlag(FileAttributes.Directory)
                    )
                    {
                        try
                        {
                            CloudFilter.DehydratePlaceholder(e.FullPath, relativePath, fileInfo.Length);
                        }
                        catch (HFileException)
                        {
                            _logger.Warn($"Unable to dehydrate placeholder for {e.FullPath} - connection may be lost");
                        }
                    }

                    if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        //var directoryInfo = new DirectoryInfo(e.FullPath);
                        //await _serverService.UpdateDirectory(directoryInfo, relativePath);
                    }
                    else
                    {
                        await _remoteService.UpdateFile(fileInfo, relativePath);
                        EnsurePlaceholderSync(e.FullPath);
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
                    var state = CloudFilter.GetPlaceholderState(e.FullPath);
                    if (state.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC))
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
                using var locker = await _fileLocker.Lock(relativePath);

                if (File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory))
                {
                    var directoryInfo = new DirectoryInfo(e.FullPath);

                    try
                    {
                        await _remoteService.CreateDirectory(directoryInfo, relativePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Create directory failed: {relativePath}", ex);
                    }

                    var childItems = Directory.EnumerateFiles(e.FullPath, "*", SearchOption.AllDirectories)
                        .Where((x) => !FileHelper.IsSystemFile(x))
                        .ToArray();
                    foreach (var childItem in childItems)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(childItem);
                            var childRelativePath = PathMapper.GetRelativePath(childItem, RootDirectory);
                            await _remoteService.CreateFile(fileInfo, childRelativePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Create file failed: {childItem}", ex);
                            continue;
                        }
                        EnsurePlaceholderSync(childItem);
                    }
                }
                else
                {
                    var fileInfo = new FileInfo(e.FullPath);
                    try
                    {
                        await _remoteService.CreateFile(fileInfo, relativePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Create file failed: {e.FullPath}", ex);
                        return;
                    }
                    EnsurePlaceholderSync(e.FullPath);
                }
            });
        };

        watcher.Error += (sender, e) => {
            var ex = e.GetException();
            _logger.Error("Client file watcher error", ex);
        };

        return watcher;
    }

    private void EnsurePlaceholderSync(string fullPath)
    {
        try
        {
            if (!CloudFilter.IsPlaceholder(fullPath))
            {
                CloudFilter.ConvertToPlaceholder(fullPath);
            }
            CloudFilter.SetInSyncState(fullPath);
            
            ShellNotify.NotifyUpdate(fullPath);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to set in-sync state for {fullPath}", ex);
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
