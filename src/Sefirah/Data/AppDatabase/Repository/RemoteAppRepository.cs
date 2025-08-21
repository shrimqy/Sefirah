using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Data.AppDatabase.Repository;

public class RemoteAppRepository(DatabaseContext context, ILogger logger)
{
    public ObservableCollection<ApplicationInfo> Applications { get; set; } = [];
    public ObservableCollection<ApplicationInfo> PinnedApplications { get; set; } = [];
    
    public event EventHandler<string>? ApplicationListUpdated;

    public async Task LoadApplicationsFromDevice(string deviceId)
    {
        await App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
        {
            Applications.Clear();
            PinnedApplications.Clear();
            
            var appEntities = context.Database.Table<ApplicationInfoEntity>()
                .ToList()
                .Where(a => HasDevice(a, deviceId))
                .OrderBy(a => a.AppName)
                .ToList();

            foreach (var entity in appEntities)
            {
                var appInfo = entity.ToApplicationInfo();
                Applications.Add(appInfo);
                
                if (appInfo.IsPinned(deviceId))
                {
                    PinnedApplications.Add(appInfo);
                }
            }
        });
    }

    public List<ApplicationInfoEntity> GetApplicationsFromDevice(string deviceId)
    {
        return context.Database.Table<ApplicationInfoEntity>()
            .ToList()
            .Where(a => HasDevice(a, deviceId))
            .OrderBy(a => a.AppName)
            .ToList();
    }

    public async Task AddOrUpdateApplication(ApplicationInfoEntity application)
    {
        var existingApp = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.PackageName == application.PackageName);

        if (existingApp != null)
        {
            // Update app info (icon, name might be different)
            existingApp.AppName = application.AppName;
            existingApp.AppIconPath = application.AppIconPath ?? existingApp.AppIconPath;
            
            // Add device to existing app
            var appInfo = existingApp.ToApplicationInfo();
            var deviceId = GetFirstDeviceId(application);
            appInfo.AddDevice(deviceId);
            existingApp.AppDeviceInfoJson = JsonSerializer.Serialize(appInfo.DeviceInfo);
            
            context.Database.Update(existingApp);

            await App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
            {
                var appToUpdate = Applications.FirstOrDefault(a => a.PackageName == existingApp.PackageName);
                if (appToUpdate != null)
                {
                    var updatedAppInfo = existingApp.ToApplicationInfo();
                    appToUpdate.PackageName = updatedAppInfo.PackageName;
                    appToUpdate.AppName = updatedAppInfo.AppName;
                    appToUpdate.IconPath = updatedAppInfo.IconPath;
                    appToUpdate.DeviceInfo = updatedAppInfo.DeviceInfo;
                }
            });
        }
        else
        {
            context.Database.Insert(application);
            await App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
            {
                var appInfo = application.ToApplicationInfo();
                Applications.Add(appInfo);
            });
        }
    }

    public async Task<NotificationFilter?> GetAppNotificationFilterAsync(string appPackage, string deviceId)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.PackageName == appPackage);
        
        if (app != null && HasDevice(app, deviceId))
        {
            var appInfo = app.ToApplicationInfo();
            var deviceInfo = appInfo.DeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
            return deviceInfo?.Filter;
        }
        return null;
    }

    public async Task<NotificationFilter> AddOrUpdateAppNotificationFilter(string deviceId, string appPackage, string? appName = null, byte[]? appIcon = null, NotificationFilter filter = NotificationFilter.ToastFeed)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.PackageName == appPackage);

        if (app != null)
        {
            var appInfo = app.ToApplicationInfo();
            appInfo.AddDevice(deviceId);
            var deviceInfo = appInfo.DeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
            deviceInfo?.Filter = filter;
            app.AppDeviceInfoJson = JsonSerializer.Serialize(appInfo.DeviceInfo);
            context.Database.Update(app);

            await App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
            {
                var appToUpdate = Applications.FirstOrDefault(a => a.PackageName == appPackage);
                if (appToUpdate != null)
                {
                    var updatedAppInfo = app.ToApplicationInfo();
                    appToUpdate.PackageName = updatedAppInfo.PackageName;
                    appToUpdate.AppName = updatedAppInfo.AppName;
                    appToUpdate.IconPath = updatedAppInfo.IconPath;
                    appToUpdate.DeviceInfo = updatedAppInfo.DeviceInfo;
                }
            });
        }
        else
        {
            string? appIconPath = null;
            if (appIcon != null)
            {
                try
                {
                    var fileName = $"{appPackage}.png";
                    appIconPath = await ImageUtils.SaveAppIconToPathAsync(appIcon, fileName).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // If saving fails, appIconPath remains null
                }
            }

            var newAppInfo = new ApplicationInfoMessage
            {
                PackageName = appPackage,
                AppName = appName ?? appPackage,
                AppIcon = appIcon != null ? Convert.ToBase64String(appIcon) : null,
            };
            
            var newEntity = await ApplicationInfoEntity.FromApplicationInfoMessage(newAppInfo, deviceId);
            context.Database.Insert(newEntity);
            
            await App.MainWindow!.DispatcherQueue.EnqueueAsync(async() =>
            {
                Applications.Add(newEntity.ToApplicationInfo());
            });
        }
        return filter;
    }

    public async void UpdateAppNotificationFilter(string deviceId, string appPackage, NotificationFilter filter)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.PackageName == appPackage);
        
        if (app != null)
        {
            var appInfo = app.ToApplicationInfo();
            var deviceInfo = appInfo.DeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
            if (deviceInfo != null)
            {
                deviceInfo.Filter = filter;
                app.AppDeviceInfoJson = JsonSerializer.Serialize(appInfo.DeviceInfo);
                context.Database.Update(app);
            }
        }
    }

    public async Task RemoveDeviceFromApplication(string appPackage, string deviceId)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.PackageName == appPackage);

        if (app != null)
        {
            var appInfo = app.ToApplicationInfo();
            appInfo.RemoveDevice(deviceId);
            app.AppDeviceInfoJson = JsonSerializer.Serialize(appInfo.DeviceInfo);
            
            if (appInfo.DeviceInfo.Count == 0)
            {
                // No more devices have this app, delete it
                context.Database.Delete(app);
                await App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
                {
                    var appToRemove = Applications.FirstOrDefault(a => a.PackageName == appPackage);
                    if (appToRemove != null)
                    {
                        Applications.Remove(appToRemove);
                        var pinnedApp = PinnedApplications.FirstOrDefault(a => a.PackageName == appPackage);
                        if (pinnedApp != null)
                        {
                            PinnedApplications.Remove(pinnedApp);
                        }
                    }
                });
            }
            else
            {
                // Still has other devices, just update
                context.Database.Update(app);
                await App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
                {
                    var appToUpdate = Applications.FirstOrDefault(a => a.PackageName == appPackage);
                    if (appToUpdate != null)
                    {
                        var updatedAppInfo = app.ToApplicationInfo();
                        appToUpdate.PackageName = updatedAppInfo.PackageName;
                        appToUpdate.AppName = updatedAppInfo.AppName;
                        appToUpdate.IconPath = updatedAppInfo.IconPath;
                        appToUpdate.DeviceInfo = updatedAppInfo.DeviceInfo;
                    }
                });
            }
        }
    }

    public async void UpdateApplicationList(PairedDevice pairedDevice, ApplicationList applicationList)
    {
        try
        {
            await RemoveAllAppsForDeviceAsync(pairedDevice.Id).ConfigureAwait(false);

            // Add/update apps from the new list
            foreach (var appInfo in applicationList.AppList)
            {
                var appEntity = await ApplicationInfoEntity.FromApplicationInfoMessage(appInfo, pairedDevice.Id);
                AddOrUpdateApplication(appEntity);
            }

            // Reload applications for this device
            await LoadApplicationsFromDevice(pairedDevice.Id);

            // Notify that the update is complete
            ApplicationListUpdated?.Invoke(this, pairedDevice.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating application list for device {DeviceId}", pairedDevice.Id);
        }
    }

    public async Task RemoveAllAppsForDeviceAsync(string deviceId)
    {
        var allApps = context.Database.Table<ApplicationInfoEntity>().ToList();
        var appsToDelete = new List<ApplicationInfoEntity>();
        
        foreach (var app in allApps)
        {
            if (HasDevice(app, deviceId))
            {
                var appInfo = app.ToApplicationInfo();
                appInfo.RemoveDevice(deviceId);
                app.AppDeviceInfoJson = JsonSerializer.Serialize(appInfo.DeviceInfo);
                
                if (appInfo.DeviceInfo.Count == 0)
                {
                    appsToDelete.Add(app);
                }
                else
                {
                    context.Database.Update(app);
                }
            }
        }
        
        // Delete apps that no longer have any devices
        foreach (var app in appsToDelete)
        {
            context.Database.Delete(app);
        }
    }

    public List<ApplicationInfoEntity> GetAllApplications()
    {
        return context.Database.Table<ApplicationInfoEntity>().ToList();
    }

    public List<ApplicationInfoEntity> GetApplicationsForDevice(string deviceId)
    {
        return context.Database.Table<ApplicationInfoEntity>()
            .ToList()
            .Where(a => HasDevice(a, deviceId))
            .ToList();
    }

    public async Task PinApp(string appPackage, string deviceId)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.PackageName == appPackage);

        if (app != null && HasDevice(app, deviceId))
        {
            var appInfo = app.ToApplicationInfo();
            appInfo.SetPinned(deviceId, true);
            app.AppDeviceInfoJson = JsonSerializer.Serialize(appInfo.DeviceInfo);
            context.Database.Update(app);

            await App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
            {
                var appToUpdate = Applications.FirstOrDefault(a => a.PackageName == appPackage);
                if (appToUpdate != null)
                {
                    var updatedAppInfo = app.ToApplicationInfo();
                    appToUpdate.PackageName = updatedAppInfo.PackageName;
                    appToUpdate.AppName = updatedAppInfo.AppName;
                    appToUpdate.IconPath = updatedAppInfo.IconPath;
                    appToUpdate.DeviceInfo = updatedAppInfo.DeviceInfo;
                    
                    if (!PinnedApplications.Contains(appToUpdate))
                    {
                        PinnedApplications.Add(appToUpdate);
                    }
                }
            });
        }
    }

    public async Task UnpinApp(string appPackage, string deviceId)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.PackageName == appPackage);

        if (app != null && HasDevice(app, deviceId))
        {
            var appInfo = app.ToApplicationInfo();
            appInfo.SetPinned(deviceId, false);
            app.AppDeviceInfoJson = JsonSerializer.Serialize(appInfo.DeviceInfo);
            context.Database.Update(app);

            await App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
            {
                var appToUpdate = Applications.FirstOrDefault(a => a.PackageName == appPackage);
                if (appToUpdate != null)
                {
                    var updatedAppInfo = app.ToApplicationInfo();
                    appToUpdate.PackageName = updatedAppInfo.PackageName;
                    appToUpdate.AppName = updatedAppInfo.AppName;
                    appToUpdate.IconPath = updatedAppInfo.IconPath;
                    appToUpdate.DeviceInfo = updatedAppInfo.DeviceInfo;
                    
                    var pinnedApp = PinnedApplications.FirstOrDefault(a => a.PackageName == appPackage);
                    if (pinnedApp != null)
                    {
                        PinnedApplications.Remove(pinnedApp);
                    }
                }
            });
        }
    }

    private bool HasDevice(ApplicationInfoEntity entity, string deviceId)
    {
        if (string.IsNullOrEmpty(entity.AppDeviceInfoJson))
            return false;
            
        try
        {
            var deviceInfo = JsonSerializer.Deserialize<List<AppDeviceInfo>>(entity.AppDeviceInfoJson) ?? [];
            return deviceInfo.Any(d => d.DeviceId == deviceId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string GetFirstDeviceId(ApplicationInfoEntity entity)
    {
        if (string.IsNullOrEmpty(entity.AppDeviceInfoJson))
            return string.Empty;
            
        try
        {
            var deviceInfo = JsonSerializer.Deserialize<List<AppDeviceInfo>>(entity.AppDeviceInfoJson) ?? [];
            return deviceInfo.FirstOrDefault()?.DeviceId ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
