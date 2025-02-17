using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;
using Sefirah.App.RemoteStorage.Commands;
using Sefirah.App.RemoteStorage.RemoteSftp;
using Sefirah.App.RemoteStorage.Worker;
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

            logger.Info("Initializing SFTP service, iP: {0}, Port: {1}, PASS: {2}, Username: {3}", info.IpAddress, info.Port, info.Password, info.Username);
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
            var localDevice = await deviceManager.GetLocalDeviceAsync();
            await Register(
                name: localDevice.DeviceName,
                directory: directory,
                accountId: localDevice.DeviceId,
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
            registrar.Unregister(info.Id);
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
            logger.Debug("Starting sync provider pool");
            syncProviderPool.Start(info);
            
            // Verify registration
            if (!registrar.IsRegistered(info.Id))
            {
                logger.Error("Sync root registration failed silently");
                throw new InvalidOperationException("Sync root registration failed without throwing exception");
            }
        }
        catch (Exception ex) 
        {
            logger.Error($"Failed to register sync root. Directory: {directory}, AccountId: {accountId}", ex);
            throw;
        }
    }
}
