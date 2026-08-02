using Sefirah.Data.Models;
using Sefirah.Platforms.Windows.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.Commands;
using Sefirah.Platforms.Windows.RemoteStorage.Sftp;
using Sefirah.Platforms.Windows.RemoteStorage.Worker;
using Sefirah.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Provider;
using Windows.System;

namespace Sefirah.Platforms.Windows.Features;

public class SftpFeature(
    ILogger logger,
    SyncRootRegistrar registrar,
    SyncProviderPool syncProviderPool,
    IUserSettingsService userSettingsService,
    ISessionManager sessionManager) : ISftpFeature
{
    private static readonly string IconDllPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "Assets\\Icons", "IconResource.dll"));

    private readonly Dictionary<string, (string Host, SftpServerInfo Info)> _sessions = [];

    public Task InitializeAsync()
    {
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        return Task.CompletedTask;
    }

    private IEnumerable<SyncRootInfo> GetSyncRootsForDevice(string deviceId)
        => registrar.GetSyncRoots().Where(r => r.Id.Contains($"!{deviceId}_"));

    private async void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (device.IsConnected) return;

        _sessions.Remove(device.Id);

        try
        {
            await StopSyncRoots(GetSyncRootsForDevice(device.Id));
        }
        catch (Exception ex)
        {
            logger.Error($"Error stopping sync roots for device {device.Id}", ex);
        }
    }

    public async Task Mount(PairedDevice device, SftpServerInfo info)
    {
        if (string.IsNullOrEmpty(device.Address)) return;

        _sessions[device.Id] = (device.Address, info);

        if (!device.DeviceSettings.StorageAccess) return;
        if (!StorageProviderSyncRootManager.IsSupported()) return;

        try
        {
            logger.Info($"Mounting SFTP for {device.Name}, IP: {device.Address}, Port: {info.Port}, Username: {info.Username}");

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
                    Host = device.Address,
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
            logger.Error($"Failed to mount SFTP for {device.Name}", ex);
        }
    }

    public async Task BrowseAsync(PairedDevice device)
    {
        if (!_sessions.TryGetValue(device.Id, out var session))
        {
            logger.Warn($"No SFTP session available to browse device {device.Name}");
            return;
        }

        try
        {
            // Prefer the cloud sync folder only while the sync provider is actively running.
            var syncRoots = GetSyncRootsForDevice(device.Id)
                .Where(r => syncProviderPool.Has(r.Id))
                .ToList();
            if (syncRoots.Count > 0)
            {
                var folderPath = syncRoots.Count == 1
                    ? syncRoots[0].Directory
                    : Path.Combine(userSettingsService.GeneralSettingsService.RemoteStoragePath, device.Name);

                await Launcher.LaunchFolderPathAsync(folderPath);
                return;
            }

            // Fallback when storage sync isn't connected (or never registered).
            await LaunchSftpUriAsync(session);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to browse device {device.Name}", ex);
        }
    }

    public async Task BrowseUriAsync(PairedDevice device)
    {
        if (!_sessions.TryGetValue(device.Id, out var session))
        {
            logger.Warn($"No SFTP session available to browse device {device.Name}");
            return;
        }

        try
        {
            await LaunchSftpUriAsync(session);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to browse device {device.Name} via SFTP URI", ex);
        }
    }

    private static async Task LaunchSftpUriAsync((string Host, SftpServerInfo Info) session)
    {
        var path = session.Info.Paths.Count == 1 ? session.Info.Paths[0] : "/";
        var uri = SftpUriHelper.CreateBrowseUri(
            session.Host,
            session.Info.Port,
            session.Info.Username,
            session.Info.Password,
            path);

        var dataPackage = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dataPackage.SetText(uri.AbsoluteUri);
        Clipboard.SetContent(dataPackage);

        if (!await Launcher.LaunchUriAsync(uri))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
    }

    public async Task RemoveAll()
    {
        _sessions.Clear();
        await StopAndUnregister(registrar.GetSyncRoots());
    }

    public async void Remove(string deviceId)
    {
        _sessions.Remove(deviceId);
        await StopAndUnregister(GetSyncRootsForDevice(deviceId));
    }

    private async Task StopSyncRoots(IEnumerable<SyncRootInfo> syncRoots)
    {
        foreach (var syncRoot in syncRoots.ToList())
        {
            await syncProviderPool.Stop(syncRoot.Id);
            logger.Info($"Stopped sync provider: {syncRoot.Id}");
        }
    }

    private async Task StopAndUnregister(IEnumerable<SyncRootInfo> syncRoots)
    {
        try
        {
            var roots = syncRoots.ToList();
            await StopSyncRoots(roots);
            foreach (var syncRoot in roots)
            {
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
                syncProviderPool.Start(syncRootInfo);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to register sync root. Directory: {directory}, AccountId: {accountId}", ex);
        }
    }
}
