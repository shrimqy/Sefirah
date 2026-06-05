using Sefirah.Data.Models;
using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.Commands;
using Sefirah.Platforms.Windows.RemoteStorage.Sftp;
using Sefirah.Platforms.Windows.RemoteStorage.Worker;
using Windows.Storage.Provider;

namespace Sefirah.Platforms.Windows.Services;

public class WindowsSftpService(
    ILogger logger,
    SyncRootRegistrar registrar,
    SyncProviderPool syncProviderPool,
    IUserSettingsService userSettingsService
    ) : ISftpService
{
    private static readonly string IconDllPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "Assets\\Icons", "IconResource.dll"));

    public async Task InitializeAsync(PairedDevice device, SftpServerInfo info)
    {
        if (!device.DeviceSettings.StorageAccess) return;
        if (!StorageProviderSyncRootManager.IsSupported()) return;

        try
        {
            logger.Info($"Initializing SFTP service, IP: {info.IpAddress}, Port: {info.Port}, Username: {info.Username}");

            var baseDirectory = userSettingsService.GeneralSettingsService.RemoteStoragePath;
            Directory.CreateDirectory(baseDirectory);

            var deviceDirectory = Path.Combine(baseDirectory, device.Name);
            Directory.CreateDirectory(deviceDirectory);

            var paths = info.Paths.Count > 0 ? info.Paths : ["/"];
            var pathNames = info.PathNames;
            var multiVolume = paths.Count > 1;

            for (int i = 0; i < paths.Count; i++)
            {
                var rawVolumeName = pathNames.Count > i ? pathNames[i] : $"Volume {i}";
                var syncRootName = multiVolume ? $"{device.Name} - {rawVolumeName}" : device.Name;
                var volumeDirectory = multiVolume ? Path.Combine(deviceDirectory, rawVolumeName) : deviceDirectory;

                Directory.CreateDirectory(volumeDirectory);

                var sftpContext = new SftpContext
                {
                    Host = info.IpAddress,
                    Port = info.Port,
                    Directory = paths[i],
                    Username = info.Username,
                    Password = info.Password,
                    WatchPeriodSeconds = 2,
                };

                await Register(syncRootName, volumeDirectory, $"{device.Id}_{i}", $"{IconDllPath},{i}", sftpContext);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize SFTP service", ex);
        }
    }

    public async void RemoveAll()
    {
        await StopAndUnregister(registrar.GetSyncRoots());
    }

    public async void Remove(string deviceId)
    {
        var syncRoots = registrar.GetSyncRoots().Where(r => r.Id.Contains($"!{deviceId}_"));
        await StopAndUnregister(syncRoots);
    }

    private async Task StopAndUnregister(IEnumerable<SyncRootInfo> syncRoots)
    {
        try
        {
            foreach (var syncRoot in syncRoots.ToList())
            {
                await syncProviderPool.Stop(syncRoot.Id);
                registrar.Unregister(syncRoot.Id);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error removing sync roots", ex);
        }
    }

    private async Task Register(string name, string directory, string accountId, string iconResource, SftpContext context)
    {
        try
        {
            var registerCommand = new RegisterSyncRootCommand
            {
                Name = name,
                Directory = directory,
                AccountId = accountId,
                PopulationPolicy = PopulationPolicy.Full,
                IconResource = iconResource,
            };

            var storageFolder = await StorageFolder.GetFolderFromPathAsync(directory);
            var syncRootInfo = registrar.Register(registerCommand, storageFolder, context);
            if (syncRootInfo is not null)
            {
                syncProviderPool.Start(syncRootInfo);
                logger.Debug($"Started sync provider for {name} ({accountId})");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to register sync root. Directory: {directory}, AccountId: {accountId}", ex);
        }
    }
}
