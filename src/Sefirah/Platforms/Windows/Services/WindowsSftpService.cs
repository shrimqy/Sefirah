using System.Security.Principal;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Platforms.Windows.RemoteStorage.Commands;
using Sefirah.Platforms.Windows.RemoteStorage.Sftp;
using Sefirah.Platforms.Windows.RemoteStorage.Worker;
using Windows.Storage.Provider;
using Windows.System.Profile;

namespace Sefirah.Platforms.Windows.Services;

public class WindowsSftpService(
    ILogger logger,
    SyncRootRegistrar registrar,
    SyncProviderPool syncProviderPool,  
    IUserSettingsService userSettingsService,
    IDeviceManager deviceManager,
    ISessionManager sessionManager
    ) : ISftpService
{
    private StorageProviderSyncRootInfo? info;

    public async Task InitializeAsync(PairedDevice device, SftpServerInfo info)
    {
        try
        {
            if (!StorageProviderSyncRootManager.IsSupported()) return;

            // Retrieve and parse the OS version from the device family version string.
            string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong major = (version & 0xFFFF000000000000L) >> 48;
            ulong minor = (version & 0x0000FFFF00000000L) >> 32;
            ulong build = (version & 0x00000000FFFF0000L) >> 16;
            ulong revision = version & 0x000000000000FFFFL;

            var currentOsVersion = new Version((int)major, (int)minor, (int)build, (int)revision);
            var requiredOsVersion = new Version(10, 0, 19624, 1000);

            // If the current OS version is lower than the threshold version, skip the sync root registration.
            if (currentOsVersion < requiredOsVersion)
            {
                logger.LogWarning(
                    "OS version {0} is lower than the required threshold {1}. Skipping sync root registration.",
                    currentOsVersion, requiredOsVersion);
                return;
            }

            logger.LogInformation("Initializing SFTP service, IP: {ip}, Port: {port}, Password: {pass}, Username: {name}",
                info.IpAddress, info.Port, info.Password, info.Username);

            var sftpContext = new SftpContext
            {
                Host = info.IpAddress,
                Port = info.Port,
                Directory = "/",
                Username = info.Username,
                Password = info.Password,
                WatchPeriodSeconds = 2,
            };

            // Parent directory for all devices
            var directory = userSettingsService.GeneralSettingsService.RemoteStoragePath;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Device-specific directory
            var deviceDirectory = Path.Combine(directory, device.Name);
            if (!Directory.Exists(deviceDirectory))
            {
                Directory.CreateDirectory(deviceDirectory);
            }
            
            await Register(
                name: device.Name,
                directory: deviceDirectory,
                accountId: device.Id,
                context: sftpContext
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize SFTP service");
            throw;
        }
    }

    public async void Remove(string deviceId)
    {
        var id = $"Shrimqy:Sefirah!{WindowsIdentity.GetCurrent().User}!{deviceId}";
        try
        {
            if (info?.Id == id)
            {
                await syncProviderPool.StopSyncRoot(info);
            }
            if (registrar.IsRegistered(id))
            {
                registrar.Unregister(id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing sync root for device {deviceId}", deviceId);
        }
    }

    private async Task Register<T>(string name, string directory, string accountId, T context) where T : struct 
    {
        try 
        {
            var registerCommand = new RegisterSyncRootCommand
            {
                Name = name,
                Directory = directory,
                AccountId = accountId,
                PopulationPolicy = PopulationPolicy.Full,
            };

            StorageFolder storageFolder = await StorageFolder.GetFolderFromPathAsync(directory);

            info = registrar.Register(registerCommand, storageFolder, context);
            if (info is not null)
            {
                syncProviderPool.Start(info);
                logger.LogDebug("Starting sync provider pool");
            }
        }
        catch (Exception ex) 
        {
            logger.LogError(ex, "Failed to register sync root. Directory: {directory}, AccountId: {accountId}", directory, accountId);
        }
    }
}
