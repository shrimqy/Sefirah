using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;
using Sefirah.App.RemoteStorage.Commands;
using Sefirah.App.RemoteStorage.RemoteSftp;
using Sefirah.App.RemoteStorage.Worker;
using System.IO;
using Windows.Storage;

namespace Sefirah.App.Services;

public class SftpService(
    ILogger logger,
    SyncRootRegistrar registrar,
    SyncProviderPool syncProviderPool,  
    IUserSettingsService userSettingsService,
    IDeviceManager deviceManager
    ) : ISftpService
{
    public async Task InitializeAsync(SftpServerInfo info)
    {
        try
        {
            logger.Info("Initializing SFTP service, iP: {0}, POrt: {1}, PASS: {2}, Username: {3}", info.IpAddress, info.Port, info.Password, info.Username);
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

            // Create directory using Windows Storage API
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

            var info = registrar.Register(registerCommand, storageFolder, context);
            syncProviderPool.Start(info);
        }
        catch (Exception ex) 
        {
            logger.Error($"Failed to register sync root. Directory: {directory}, AccountId: {accountId}", ex);
            throw;
        }
    }
}
