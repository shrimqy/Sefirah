using Microsoft.Extensions.Hosting;
using Sefirah.App.RemoteStorage.Async;
using Sefirah.App.RemoteStorage.Shell;
using Sefirah.App.RemoteStorage.Worker;
using Sefirah.Common.Utils;

namespace Sefirah.App.Helpers;

public sealed class ShellWorker(
    ShellRegistrar shellRegistrar,
    SyncRootRegistrar syncRootRegistrar,
    ILogger logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.Info("Starting shell worker");
            // Use RegisterUntilCancelled which handles COM initialization properly
            shellRegistrar.RegisterUntilCancelled(stoppingToken);

            // Wait for cancellation
            await stoppingToken;
        }
        catch (Exception ex)
        {
            logger.Error("Failed to execute shell worker", ex);
            throw;
        }
    }
}
