using Microsoft.Extensions.Options;
using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.Commands;
using Sefirah.App.RemoteStorage.Configuration;
using Sefirah.App.RemoteStorage.Interop;
using Sefirah.Common.Utils;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Provider;

namespace Sefirah.App.RemoteStorage.Worker;
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

        string iconPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets", "IconResource.dll"));
        
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

        logger.Debug("Registering {syncRootId}", id);
        StorageProviderSyncRootManager.Register(info);

        return info;
    }

    public void Unregister(string id)
    {
        logger.Debug("Unregistering {syncRootId}", id);
        try
        {
            // Try to force garbage collection and cleanup before unregistering
            GC.Collect();
            GC.WaitForPendingFinalizers();
            StorageProviderSyncRootManager.Unregister(id);

        }
        catch (COMException ex) when (ex.HResult == -2147023728)
        {
            logger.Error("Sync root not found", ex);
        }
        catch (Exception ex)
        {
            logger.Error("Unregister sync root failed", ex);
        }
    }

    public void UpdateCredentials<T>(string id, T context) where T : struct
    {
        var roots = StorageProviderSyncRootManager.GetCurrentSyncRoots();
        var existingRoot = roots.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"Sync root {id} not found");
        var contextBytes = StructBytes.ToBytes(context);
        
        // Update using the lower-level API
        CloudFilter.UpdateSyncRoot(existingRoot.Path.Path, contextBytes);
    }
}
