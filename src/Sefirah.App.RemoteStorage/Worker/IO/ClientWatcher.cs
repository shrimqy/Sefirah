using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.Helpers;
using Sefirah.App.RemoteStorage.Interop;
using Sefirah.App.RemoteStorage.Interop.Extensions;
using Sefirah.App.RemoteStorage.RemoteAbstractions;
using Sefirah.Common.Utils;
using System.Threading.Channels;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;

namespace Sefirah.App.RemoteStorage.Worker.IO;
public class ClientWatcher : IDisposable
{
    private readonly ISyncProviderContextAccessor _contextAccessor;
    private readonly ChannelWriter<Func<Task>> _taskWriter;
    private readonly FileLocker _fileLocker;
    private readonly IRemoteReadWriteService _remoteService;
    private readonly PlaceholdersService _placeholdersService;
    private readonly ILogger _logger;
    private readonly FileSystemWatcher _watcher;

    private string _rootDirectory => _contextAccessor.Context.RootDirectory;

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
        var watcher = new FileSystemWatcher(_rootDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.Attributes
                | NotifyFilters.LastWrite,
            InternalBufferSize = 64 * 1024,
        };

        watcher.Changed += async (object sender, FileSystemEventArgs e) => {
            try 
            {
                if (e.ChangeType != WatcherChangeTypes.Changed || 
                    !Path.Exists(e.FullPath) || 
                    FileHelper.IsSystemFile(e.FullPath))
                {
                    return;
                }

                var fileInfo = new FileInfo(e.FullPath);
                
                CldApi.CF_PLACEHOLDER_STATE state;
                try 
                {
                    state = CloudFilter.GetPlaceholderState(e.FullPath);
                }
                catch (HFileException)
                {
                    // File handle is invalid, likely due to disconnection
                    _logger.Warn("Unable to get placeholder state for {path} - connection may be lost", e.FullPath);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Error( "Error getting placeholder state for {path}", e.FullPath, ex);
                    return;
                }

                // More specific conditions for when to skip the change event
                if (state.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC) ||
                    state.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PLACEHOLDER) ||
                    fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                    fileInfo.LastWriteTime == fileInfo.LastAccessTime ||
                    e.ChangeType == WatcherChangeTypes.Changed && 
                    (state == CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_NO_STATES || 
                     state == (CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PLACEHOLDER | CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC)))
                {
                    return;
                }

                await _taskWriter.WriteAsync(async () => {
                    var relativePath = PathMapper.GetRelativePath(e.FullPath, _rootDirectory);
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
                                        _logger.Warn("Unable to hydrate placeholder for {path} - connection may be lost", childItem);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.Error("Hydrate file failed: {filePath}", childItem, ex);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("Error processing directory {path}", e.FullPath, ex);
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
                                _logger.Warn("Unable to hydrate placeholder for {path} - connection may be lost", e.FullPath);
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
                            _logger.Warn("Unable to dehydrate placeholder for {path} - connection may be lost", e.FullPath);
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
                    }
                    
                });
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled error in file system watcher for {path}", e.FullPath, ex);
            }
        };

        watcher.Created += async (object sender, FileSystemEventArgs e) => {

            var state = CloudFilter.GetPlaceholderState(e.FullPath);
            if (state.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC))
            {
                return;
            }

            await _taskWriter.WriteAsync(async () => {
                var relativePath = PathMapper.GetRelativePath(e.FullPath, _rootDirectory);
                using var locker = await _fileLocker.Lock(relativePath);

                if (File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory))
                {
                    var directoryInfo = new DirectoryInfo(e.FullPath);
                    await _remoteService.CreateDirectory(directoryInfo, relativePath);
                    var childItems = Directory.EnumerateFiles(e.FullPath, "*", SearchOption.AllDirectories)
                        .Where((x) => !FileHelper.IsSystemFile(x))
                        .ToArray();
                    foreach (var childItem in childItems)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(childItem);
                            await _remoteService.CreateFile(fileInfo, childItem);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Create file failed: {filePath}", childItem, ex);
                        }
                    }
                }
                else
                {
                    try
                    {
                        var fileInfo = new FileInfo(e.FullPath);
                        await _remoteService.CreateFile(fileInfo, relativePath);

                        // Add explicit placeholder and sync state handling with delays
                        try
                        {
                            _logger.Info("Setting placeholder state for new file: {path}", e.FullPath);
                            
                            if (!CloudFilter.IsPlaceholder(e.FullPath))
                            {
                                CloudFilter.ConvertToPlaceholder(e.FullPath);
                                _logger.Info("Converted to placeholder: {path}", e.FullPath);
                                
                                // Give time for the placeholder conversion to settle
                                await Task.Delay(1000);
                            }

                            var stateAfterPlaceholder = CloudFilter.GetPlaceholderState(e.FullPath);
                            _logger.Info("State after placeholder conversion: {state} for {path}", 
                                stateAfterPlaceholder, e.FullPath);

                            // Set sync state and wait for it to settle
                            CloudFilter.SetInSyncState(e.FullPath);
                            await Task.Delay(1000);  // Wait for state change to complete

                            var finalState = CloudFilter.GetPlaceholderState(e.FullPath);
                            _logger.Info("Final state after sync: {state} for {path}", 
                                finalState, e.FullPath);

                            // One final check to ensure we don't trigger another upload
                            if (!finalState.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC))
                            {
                                _logger.Warn("Sync state not set properly, retrying for: {path}", e.FullPath);
                                CloudFilter.SetInSyncState(e.FullPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Failed to set placeholder/sync state for {path}", e.FullPath, ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Create file failed: {filePath}", e.FullPath, ex);
                    }
                }
            });
        };

        watcher.Error += (object sender, ErrorEventArgs e) => {
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