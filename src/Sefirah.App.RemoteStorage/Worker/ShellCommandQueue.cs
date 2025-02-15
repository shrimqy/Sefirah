using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.Helpers;
using Sefirah.App.RemoteStorage.Interop;
using Sefirah.App.RemoteStorage.RemoteAbstractions;
using Sefirah.Common.Utils;
using System.Threading.Channels;
using static Vanara.PInvoke.CldApi;

namespace Sefirah.App.RemoteStorage.Worker;
public sealed class ShellCommandQueue(
    ISyncProviderContextAccessor contextAccessor,
    ChannelReader<ShellCommand> taskReader,
    FileLocker fileLocker,
    PlaceholdersService placeholderService,
    IRemoteReadWriteService remoteService,
    ILogger logger
) : IDisposable
{
    private string _rootDirectory => contextAccessor.Context.RootDirectory;
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
                    if (!shellCommand.FullPath.StartsWith(_rootDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var state = CloudFilter.GetPlaceholderState(shellCommand.FullPath);                    
                    // Broken upload, state is just "No State"
                    logger.Info("placeholder state {state}", state);
                    if (state == CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_NO_STATES ||
                        state == CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC ||
                        state == CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PLACEHOLDER)
                    {
                        var isDirectory = File.GetAttributes(shellCommand.FullPath).HasFlag(FileAttributes.Directory);
                        var relativePath = PathMapper.GetRelativePath(shellCommand.FullPath, _rootDirectory);
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
                                logger.Info("Set sync state after upload for {path}", shellCommand.FullPath);
                            }
                            catch (Exception ex)
                            {
                                logger.Error("Failed to set sync state for {path}", shellCommand.FullPath, ex);
                            }
                        }
                    }
                    else if (state.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PARTIALLY_ON_DISK))
                    {
                        var isDirectory = File.GetAttributes(shellCommand.FullPath).HasFlag(FileAttributes.Directory);
                        var relativePath = PathMapper.GetRelativePath(shellCommand.FullPath, _rootDirectory);
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
