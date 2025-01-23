using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sefirah.App.RemoteStorage.Async;
using Sefirah.App.RemoteStorage.Shell;
using Sefirah.App.RemoteStorage.Worker;

namespace Sefirah.App.Helpers;

public sealed class ShellWorker(
    ShellRegistrar shellRegistrar,
    SyncRootRegistrar syncRootRegistrar,
    ILogger<ShellWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting shell worker");
            // Use RegisterUntilCancelled which handles COM initialization properly
            shellRegistrar.RegisterUntilCancelled(stoppingToken);

            // Wait for cancellation
            await stoppingToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute shell worker");
            throw;
        }
    }
}
