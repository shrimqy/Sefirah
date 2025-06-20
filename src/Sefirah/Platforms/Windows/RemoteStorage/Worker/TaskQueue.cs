using System.Threading.Channels;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;
public sealed class TaskQueue(ChannelReader<Func<Task>> taskReader) : IDisposable
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
                await func();
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
