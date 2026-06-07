using System.Threading.Channels;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using static Vanara.PInvoke.CldApi;
using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;

public sealed partial class ShellCommandQueue(
    ISyncProviderContextAccessor contextAccessor,
    ChannelReader<ShellCommand> taskReader,
    FileLocker fileLocker,
    PlaceholdersService placeholderService,
    IRemoteReadWriteService remoteService,
    ILogger logger
) : IDisposable
{
    private string RootDirectory => contextAccessor.Context.RootDirectory;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private Task? _runningTask = null;

    public void Start(CancellationToken stoppingToken)
    {
        var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _disposeTokenSource.Token).Token;
        _runningTask = Task.Factory.StartNew(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var shellCommand = await taskReader.ReadAsync(cancellationToken);
                try
                {
                    if (!shellCommand.FullPath.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var state = CloudFilter.GetPlaceholderState(shellCommand.FullPath);                    
                    // Broken upload, state is just "No State"
                    logger.Info($"placeholder state {state}");
                    if (state is CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_NO_STATES ||
                        state is CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC ||
                        state is CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PLACEHOLDER)
                    {
                        var isDirectory = File.GetAttributes(shellCommand.FullPath).HasFlag(FileAttributes.Directory);
                        var relativePath = PathMapper.GetRelativePath(shellCommand.FullPath, RootDirectory);
                        using var locker = await fileLocker.Lock(relativePath);
                        
                        if (isDirectory)
                        {
                            await placeholderService.CreateOrUpdateDirectory(relativePath);
                        }
                        else
                        {
                            var fileInfo = new FileInfo(shellCommand.FullPath);
                            if (remoteService.Exists(relativePath))
                            {
                                await placeholderService.CreateOrUpdateFile(relativePath);
                            }
                            else
                            {
                                await remoteService.CreateFile(fileInfo, relativePath);
                            }
                            
                            // Add explicit sync state setting here
                            try
                            {
                                CloudFilter.SetInSyncState(shellCommand.FullPath);
                                logger.Info($"Set sync state after upload for {shellCommand.FullPath}");
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Failed to set sync state for {shellCommand.FullPath}", ex);
                            }
                        }
                    }
                    else if (state.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PARTIALLY_ON_DISK))
                    {
                        var isDirectory = File.GetAttributes(shellCommand.FullPath).HasFlag(FileAttributes.Directory);
                        var relativePath = PathMapper.GetRelativePath(shellCommand.FullPath, RootDirectory);
                        if (isDirectory)
                        {
                            await placeholderService.CreateOrUpdateDirectory(relativePath);
                        }
                        else
                        {
                            await placeholderService.CreateOrUpdateFile(relativePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Error in handling shell command", ex);
                }
            }
        });
    }

    public Task Stop()
    {
        _disposeTokenSource.Cancel();
        return _runningTask ?? Task.CompletedTask;
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }
}
