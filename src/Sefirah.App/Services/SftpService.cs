using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;
using Sefirah.App.RemoteStorage.Commands;
using Sefirah.App.RemoteStorage.Configuration;
using Sefirah.App.RemoteStorage.RemoteSftp;
using Sefirah.App.RemoteStorage.Worker;
using System.Security.Principal;
using Windows.Storage;
using Windows.Storage.Provider;

namespace Sefirah.App.Services;

public class SftpService(
    ILogger logger,
    SyncRootRegistrar registrar,
    SyncProviderPool syncProviderPool,  
    IUserSettingsService userSettingsService,
    IDeviceManager deviceManager,
    ISessionManager sessionManager
    ) : ISftpService
{
    private StorageProviderSyncRootInfo? info;

    public async Task InitializeAsync(SftpServerInfo info)
    {
        try
        {
            if (!StorageProviderSyncRootManager.IsSupported())
            {
                return;
            }

            // Retrieve and parse the OS version from the device family version string.
            string deviceFamilyVersion = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong major = (version & 0xFFFF000000000000L) >> 48;
            ulong minor = (version & 0x0000FFFF00000000L) >> 32;
            ulong build = (version & 0x00000000FFFF0000L) >> 16;
            ulong revision = (version & 0x000000000000FFFFL);

            var currentOsVersion = new Version((int)major, (int)minor, (int)build, (int)revision);
            var requiredOsVersion = new Version(10, 0, 19624, 1000);

            // If the current OS version is lower than the threshold version, skip the sync root registration.
            if (currentOsVersion < requiredOsVersion)
            {
                logger.Info(
                    "OS version {0} is lower than the required threshold {1}. Skipping sync root registration.",
                    currentOsVersion, requiredOsVersion);
                return;
            }

            logger.Info("Initializing SFTP service, iP: {0}, Port: {1}, Password: {2}, Username: {3}",
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

            var directory = userSettingsService.FeatureSettingsService.RemoteStoragePath;
            var remoteDevice = await deviceManager.GetLastConnectedDevice();
            await Register(
                name: remoteDevice!.Name,
                directory: directory,
                accountId: remoteDevice!.DeviceId,
                context: sftpContext
            );
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize SFTP service", ex);
            throw;
        }

        sessionManager.ClientConnectionStatusChanged += OnClientConnectionStatusChanged;
    }

    private void OnClientConnectionStatusChanged(object? sender, ConnectedSessionEventArgs e)
    {
        if (!e.IsConnected && info != null)
        {
            syncProviderPool.StopSyncRoot(info);
            //registrar.Unregister(info.Id);
        }
    }

    public void RemoveSyncRoot(string deviceId)
    {
        var id = $"Shrimqy:Sefirah!{WindowsIdentity.GetCurrent().User}!{deviceId}";
        if (info?.Id == id)
        {
            syncProviderPool.StopSyncRoot(info);
        }
        if (registrar.IsRegistered(id))
        {
            registrar.Unregister(id);
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

            StorageFolder parentFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(registerCommand.Directory));
            StorageFolder storageFolder;
            try
            {
                storageFolder = await parentFolder.GetFolderAsync(Path.GetFileName(registerCommand.Directory));
            }
            catch (FileNotFoundException)
            {
                storageFolder = await parentFolder.CreateFolderAsync(Path.GetFileName(registerCommand.Directory));
            }

            info = registrar.Register(registerCommand, storageFolder, context);
            if (info != null)
            {
                syncProviderPool.Start(info);
                logger.Debug("Starting sync provider pool");
            }
        }
        catch (Exception ex) 
        {
            logger.Error($"Failed to register sync root. Directory: {directory}, AccountId: {accountId}", ex);
        }
    }
}
