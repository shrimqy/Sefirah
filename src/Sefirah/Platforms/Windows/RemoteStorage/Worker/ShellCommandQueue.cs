using System.Threading.Channels;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;

public sealed partial class ShellCommandQueue(
    ISyncProviderContextAccessor contextAccessor,
    ChannelReader<ShellCommand> taskReader,
    ChannelWriter<ShellCommand> taskWriter,
    ShellCommandRouter router,
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
        router.Register(RootDirectory, taskWriter);
        _runningTask = Task.Factory.StartNew(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var shellCommand = await taskReader.ReadAsync(cancellationToken);
                try
                {
                    var relativePath = PathMapper.GetRelativePath(shellCommand.FullPath, RootDirectory);
                    switch (shellCommand.Kind)
                    {
                        case ShellCommandKind.Sync:
                            await Sync(relativePath);
                            break;
                        case ShellCommandKind.Download:
                            await Download(relativePath);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Error in handling shell command {shellCommand.Kind} for {shellCommand.FullPath}", ex);
                }
            }
        });
    }

    private async Task Sync(string relativePath)
    {
        if (Directory.Exists(Path.Join(RootDirectory, relativePath)))
        {
            if (remoteService.Exists(relativePath))
            {
                await SyncDirectory(relativePath);
            }
            await placeholderService.SyncPendingChanges(relativePath);
            return;
        }

        using var locker = await fileLocker.Lock(relativePath);
        await placeholderService.UpdateFile(relativePath);
    }

    private async Task SyncDirectory(string relativeDirectory)
    {
        var clientDirectory = Path.Join(RootDirectory, relativeDirectory);
        if (File.GetAttributes(clientDirectory).HasFlag(FileAttributes.Offline))
        {
            return;
        }

        foreach (var remoteFile in remoteService.EnumerateFiles(relativeDirectory))
        {
            using var locker = await fileLocker.Lock(remoteFile.RelativePath);
            try
            {
                await placeholderService.CreateOrUpdateFile(remoteFile.RelativePath);
            }
            catch (Exception ex)
            {
                logger.Error($"Sync failed for {remoteFile.RelativePath}", ex);
            }
        }

        foreach (var remoteDirectory in remoteService.EnumerateDirectories(relativeDirectory))
        {
            try
            {
                using (await fileLocker.Lock(remoteDirectory.RelativePath))
                {
                    await placeholderService.CreateOrUpdateDirectory(remoteDirectory.RelativePath);
                }
                await SyncDirectory(remoteDirectory.RelativePath);
            }
            catch (Exception ex)
            {
                logger.Error($"Sync failed for {remoteDirectory.RelativePath}", ex);
            }
        }
    }

    private async Task Download(string relativePath)
    {
        using var locker = await fileLocker.Lock(relativePath);
        placeholderService.Hydrate(relativePath);
    }

    public Task Stop()
    {
        router.Unregister(RootDirectory, taskWriter);
        _disposeTokenSource.Cancel();
        return _runningTask ?? Task.CompletedTask;
    }

    public void Dispose()
    {
        router.Unregister(RootDirectory, taskWriter);
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }
}
