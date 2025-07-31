using System.Runtime.InteropServices.WindowsRuntime;
using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.Commands;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using Vanara.PInvoke;
using Windows.Storage.Provider;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;
public class SyncProviderPool(
    IServiceScopeFactory scopeFactory,
    ILogger logger)
{
    private readonly Dictionary<string, CancellableThread> _threads = [];
    private readonly object _lock = new();
    private bool _stopping = false;

    public void Start(StorageProviderSyncRootInfo syncRootInfo)
    {
        if (_stopping)
        {
            return;
        }

        lock (_lock)
        {
            // If there's an existing thread, stop it first
            if (_threads.TryGetValue(syncRootInfo.Id, out var existingThread))
            {
                logger.LogDebug("Stopping existing sync provider for {id}", syncRootInfo.Id);
                existingThread.Stop().Wait();
                _threads.Remove(syncRootInfo.Id);
            }

            var thread = new CancellableThread((cancellation) => 
                Run(syncRootInfo, cancellation), logger);
            
            thread.Stopped += (sender, e) => {
                lock (_lock)
                {
                    _threads.Remove(syncRootInfo.Id);
                    (sender as CancellableThread)?.Dispose();
                }
            };

            thread.Start();
            _threads[syncRootInfo.Id] = thread;
            logger.LogDebug("Started new sync provider for {id}", syncRootInfo.Id);
        }
    }

    public bool Has(string id) => _threads.ContainsKey(id);

    public async Task StopAll()
    {
        _stopping = true;

        var stopTasks = _threads.Values.Select((thread) => thread.Stop()).ToArray();
        await Task.WhenAll(stopTasks);
    }

    public async Task StopSyncRoot(StorageProviderSyncRootInfo syncRootInfo)
    {
        try
        {
            if (_threads.TryGetValue(syncRootInfo.Id, out var existingThread))
            {
                logger.LogDebug("Stopping existing sync provider for {id}", syncRootInfo.Id);
                await existingThread.Stop();
                _threads.Remove(syncRootInfo.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop sync root");
        }
    }

    public async Task Stop(string id)
    {
        if (!_threads.TryGetValue(id, out var thread))
        {
            return;
        }
        await thread.Stop();
    }

    private async Task Run(StorageProviderSyncRootInfo syncRootInfo, CancellationToken cancellation)
    {
        using var scope = scopeFactory.CreateScope();
        var contextAccessor = scope.ServiceProvider.GetRequiredService<SyncProviderContextAccessor>();
        contextAccessor.Context = new SyncProviderContext
        {
            Id = syncRootInfo.Id,
            RootDirectory = syncRootInfo.Path.Path,
            PopulationPolicy = (PopulationPolicy)syncRootInfo.PopulationPolicy,
        };
        var remoteContextSetter = scope.ServiceProvider.GetServices<IRemoteContextSetter>()
            .Single((setter) => setter.RemoteKind == contextAccessor.Context.RemoteKind);
        remoteContextSetter.SetRemoteContext(syncRootInfo.Context.ToArray());

        var syncProvider = scope.ServiceProvider.GetRequiredService<SyncProvider>();
        await syncProvider.Run(cancellation);
    }

    private sealed class CancellableThread : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;
        public event EventHandler? Stopped;

        public CancellableThread(Func<CancellationToken, Task> action, ILogger logger)
        {
            _task = new Task(async () => {
                try
                {
                    await action(_cts.Token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Thread stopped unexpectedly");
                }
                Stopped?.Invoke(this, EventArgs.Empty);
            });
        }

        public static CancellableThread CreateAndStart(Func<CancellationToken, Task> action, ILogger logger)
        {
            var cans = new CancellableThread(action, logger);
            cans.Start();
            return cans;
        }

        public void Start()
        {
            _task.Start();
        }

        public async Task Stop()
        {
            _cts.Cancel();
            await _task;

        }
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
