using System.Threading.Channels;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;

public sealed partial class TaskQueue(ChannelReader<Func<Task>> taskReader, ILogger logger) : IDisposable
{
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private Task? _runningTask = null;

    public void Start(CancellationToken stoppingToken)
    {
        var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _disposeTokenSource.Token).Token;
        _runningTask = Task.Factory.StartNew(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var func = await taskReader.ReadAsync(cancellationToken);
                // A throwing task must not end the loop, or every later sync event for this root is silently dropped
                try
                {
                    await func();
                }
                catch (Exception ex)
                {
                    logger.Error("Sync task failed", ex);
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
