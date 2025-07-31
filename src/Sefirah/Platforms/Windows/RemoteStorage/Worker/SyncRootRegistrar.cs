using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Options;
using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.RemoteStorage.Commands;
using Sefirah.Platforms.Windows.RemoteStorage.Configuration;
using Windows.Security.Cryptography;
using Windows.Storage.Provider;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;
public class SyncRootRegistrar(
    IOptions<ProviderOptions> providerOptions,
    ILogger logger
)
{
    public IReadOnlyList<SyncRootInfo> GetSyncRoots()
    {
        var roots = StorageProviderSyncRootManager.GetCurrentSyncRoots();
        return roots
            .Where((x) => x.Id.StartsWith(providerOptions.Value.ProviderId + "!"))
            .Select((x) => new SyncRootInfo
            {
                Id = x.Id,
                Name = x.DisplayNameResource,
                Directory = x.Path.Path,
            })
            .ToArray();
    }

    public bool IsRegistered(string id) =>
        StorageProviderSyncRootManager.GetCurrentSyncRoots().Any((x) => x.Id == id);

    public StorageProviderSyncRootInfo Register<T>(RegisterSyncRootCommand command, IStorageFolder directory, T context) where T : struct
    {
        // Stage 1: Setup
        //--------------------------------------------------------------------------------------------
        // The client folder (syncroot) must be indexed in order for states to properly display
        var clientDirectory = new DirectoryInfo(command.Directory);
        clientDirectory.Attributes &= ~System.IO.FileAttributes.NotContentIndexed;

        var id = $"{providerOptions.Value.ProviderId}!{WindowsIdentity.GetCurrent().User}!{command.AccountId}";

        var contextBytes = StructBytes.ToBytes(context);

        if (IsRegistered(id))
        {
            UpdateCredentials(id, context);
        }

        string iconPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets\\Icons", "IconResource.dll"));
        
        var info = new StorageProviderSyncRootInfo
        {
            Id = id,
            Path = directory,
            DisplayNameResource = command.Name,
            IconResource = $"{iconPath},0",
            HydrationPolicy = StorageProviderHydrationPolicy.Full,
            HydrationPolicyModifier = StorageProviderHydrationPolicyModifier.AutoDehydrationAllowed |
                                     StorageProviderHydrationPolicyModifier.AllowFullRestartHydration,
            PopulationPolicy = (StorageProviderPopulationPolicy)command.PopulationPolicy,
            InSyncPolicy = StorageProviderInSyncPolicy.FileCreationTime | 
                           StorageProviderInSyncPolicy.DirectoryCreationTime |
                           StorageProviderInSyncPolicy.FileLastWriteTime |
                           StorageProviderInSyncPolicy.DirectoryLastWriteTime |
                           StorageProviderInSyncPolicy.PreserveInsyncForSyncEngine |
                           StorageProviderInSyncPolicy.Default,
            ShowSiblingsAsGroup = false,
            Version = "1.0.0",
            //HardlinkPolicy = StorageProviderHardlinkPolicy.None,
            // RecycleBinUri = new Uri(""),
            Context = CryptographicBuffer.CreateFromByteArray(contextBytes),
        };
         //info.StorageProviderItemPropertyDefinitions.Add()

        logger.LogDebug("Registering {syncRootId}", id);
        StorageProviderSyncRootManager.Register(info);

        return info;
    }

    public void Unregister(string id)
    {
        logger.LogDebug("Unregistering {syncRootId}", id);
        try
        {
            // Try to force garbage collection and cleanup before unregistering
            GC.Collect();
            GC.WaitForPendingFinalizers();
            StorageProviderSyncRootManager.Unregister(id);

        }
        catch (COMException ex) when (ex.HResult == -2147023728)
        {
            logger.LogError(ex, "Sync root not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unregister sync root failed");
        }
    }

    public void UpdateCredentials<T>(string id, T context) where T : struct
    {
        try
        {
            var roots = StorageProviderSyncRootManager.GetCurrentSyncRoots();
            var existingRoot = roots.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"Sync root {id} not found");
            var contextBytes = StructBytes.ToBytes(context);

            CloudFilter.UpdateSyncRoot(existingRoot.Path.Path, contextBytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update credentials");
            throw;
        }
    }
}
