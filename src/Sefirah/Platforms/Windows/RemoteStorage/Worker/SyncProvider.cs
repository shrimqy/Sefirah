using Sefirah.Platforms.Windows.Async;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.Commands;
using Sefirah.Platforms.Windows.RemoteStorage.Worker.IO;
using static Vanara.PInvoke.CldApi;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;
public class SyncProvider(
    ISyncProviderContextAccessor contextAccessor,
    TaskQueue taskQueue,
    ShellCommandQueue shellCommandQueue,
    SyncRootConnector syncProvider,
    PlaceholdersService placeholdersService,
    ClientWatcher clientWatcher,
    RemoteWatcher remoteWatcher,
    ILogger logger
)
{
    public async Task Run(CancellationToken cancellation)
    {
        taskQueue.Start(cancellation);
        shellCommandQueue.Start(cancellation);

        // Hook up callback methods (in this class) for transferring files between client and server
        using var connectDisposable = new Disposable<CF_CONNECTION_KEY>(syncProvider.Connect(), syncProvider.Disconnect);

        // Create the placeholders in the client folder so the user sees something
        if (contextAccessor.Context.PopulationPolicy == PopulationPolicy.AlwaysFull)
        {
            placeholdersService.CreateBulk(string.Empty);
        }

        syncProvider.UpdatePlaceholders(contextAccessor.Context.RootDirectory);

        // Stage 2: Running
        //--------------------------------------------------------------------------------------------
        // The file watcher loop for this sample will run until the user presses Ctrl-C.
        // The file watcher will look for any changes on the files in the client (syncroot) in order
        // to let the cloud know.
        clientWatcher.Start();
        remoteWatcher.Start(cancellation);

        // Run until SIGTERM
        await cancellation;

        await shellCommandQueue.Stop();

        await taskQueue.Stop();

        logger.LogDebug("Disconnecting...");
    }
}
