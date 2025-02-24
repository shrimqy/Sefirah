using Microsoft.Extensions.Hosting;
using Sefirah.App.RemoteStorage.Async;
using Sefirah.App.RemoteStorage.Helpers;
using Sefirah.App.RemoteStorage.Shell;
using Sefirah.App.RemoteStorage.Worker;
using Sefirah.Common.Utils;

namespace Sefirah.App.Helpers;

public sealed class ShellWorker(
    ShellRegistrar shellRegistrar,
    ILogger logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.Info("Starting shell worker");

            // Start up the task that registers and hosts the services for the shell (such as custom states, menus, etc)
            shellRegistrar.RegisterUntilCancelled(stoppingToken);

            await stoppingToken;
        }
        catch (Exception ex)
        {
            logger.Error("Failed to execute shell worker", ex);
        }
    }
}
