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
        => registrar.GetSyncRoots().Where(r => r.Id.Contains($"!{deviceId}_") || r.Id.EndsWith($"!{deviceId}"));

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

            // Remove roots of volumes the device no longer shares
            var currentIds = Enumerable.Range(0, paths.Count).Select(i => registrar.GetId($"{device.Id}_{i}")).ToHashSet();
            await StopAndUnregister(GetSyncRootsForDevice(device.Id).Where(r => !currentIds.Contains(r.Id)));

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

                await Register(syncRootName, volumeDirectory, $"{device.Id}_{i}", $"{IconDllPath},{i}", sftpContext, multiVolume);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to mount SFTP for {device.Name}", ex);
        }
    }

    public async Task BrowseAsync(PairedDevice device)
    {
        try
        {
            var syncRoots = GetSyncRootsForDevice(device.Id).ToList();
            if (syncRoots.Count > 0)
            {
                var folderPath = syncRoots.Count == 1
                    ? syncRoots[0].Directory
                    : Path.Combine(userSettingsService.GeneralSettingsService.RemoteStoragePath, device.Name);

                await Launcher.LaunchFolderPathAsync(folderPath);
                return;
            }

            // Fallback when storage sync isn't registered
            if (!_sessions.TryGetValue(device.Id, out var session))
                return;

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

    private async Task Register(string name, string directory, string accountId, string iconResource, SftpContext context, bool multiVolume)
    {
        try
        {
            // Re-register when the device was renamed or the storage path changed
            var id = registrar.GetId(accountId);
            var existing = registrar.GetSyncRoots().FirstOrDefault(r => r.Id == id);
            if (existing is not null && (!PathsEqual(existing.Directory, directory) || existing.Name != name))
            {
                await syncProviderPool.Stop(id);
                registrar.Unregister(id);
                if (!PathsEqual(existing.Directory, directory))
                {
                    MoveSyncRootDirectory(existing.Directory, directory, multiVolume);
                }
            }

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

    /// <summary>
    /// Moves the old sync root folder to the new path, leaving it in place if the move fails.
    /// With <paramref name="removeEmptyParent"/>, the old device folder is deleted once empty.
    /// </summary>
    private void MoveSyncRootDirectory(string oldDirectory, string newDirectory, bool removeEmptyParent)
    {
        if (!Directory.Exists(oldDirectory))
        {
            return;
        }

        try
        {
            if (Directory.Exists(newDirectory))
            {
                if (Directory.EnumerateFileSystemEntries(newDirectory).Any())
                {
                    logger.Warn($"Not moving {oldDirectory} to {newDirectory}, the target isn't empty");
                    return;
                }
                Directory.Delete(newDirectory);
            }
            Directory.Move(oldDirectory, newDirectory);
            logger.Info($"Moved sync root {oldDirectory} to {newDirectory}");
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to move {oldDirectory} to {newDirectory}, leaving it in place", ex);
            Directory.CreateDirectory(newDirectory);
            return;
        }

        var oldParent = Path.GetDirectoryName(oldDirectory);
        if (!removeEmptyParent || oldParent is null || !Directory.Exists(oldParent))
        {
            return;
        }
        try
        {
            if (!Directory.EnumerateFileSystemEntries(oldParent).Any())
            {
                Directory.Delete(oldParent);
            }
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to remove empty folder {oldParent}", ex);
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
}
