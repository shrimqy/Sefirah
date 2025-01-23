using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sefirah.App.RemoteStorage.Async;
using Sefirah.App.RemoteStorage.Configuration;
using Windows.Storage.Provider;

namespace Sefirah.App.RemoteStorage.Worker;
public class SyncProviderWorker(
    IOptions<ProviderOptions> providerOptions,
    SyncProviderPool pool
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConnectAll();

        await stoppingToken;

        await pool.StopAll();
    }

    private void ConnectAll()
    {
        var syncRootsInfos = StorageProviderSyncRootManager.GetCurrentSyncRoots()
            .Where(x => x.Id.StartsWith($"{providerOptions.Value.ProviderId}!"))
            .ToArray();

        foreach (var syncRootInfo in syncRootsInfos)
        {
            if (pool.Has(syncRootInfo.Id))
            {
                continue;
            }

            pool.Start(syncRootInfo);
        }
    }
}
