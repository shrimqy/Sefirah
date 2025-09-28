using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class RemoteAppRepository(DatabaseContext context, ILogger logger)
{
    public ObservableCollection<ApplicationInfo> Applications { get; set; } = [];
    
    public event EventHandler<string>? ApplicationListUpdated;

    public async Task LoadApplicationsFromDevice(string deviceId)
    {
        await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            Applications.Clear();

            var appEntities = context.Database.Table<ApplicationInfoEntity>()
                .ToList()
                .Where(a => HasDevice(a, deviceId))
                .OrderBy(a => a.AppName)
                .ToList();

            foreach (var entity in appEntities)
            {
                var appInfo = entity.ToApplicationInfo(deviceId);
                Applications.Add(appInfo);
            }
        });
    }

    public ObservableCollection<ApplicationInfo> GetApplicationsForDevice(string deviceId)
    {
        return context.Database.Table<ApplicationInfoEntity>()
            .ToList()
            .Where(a => HasDevice(a, deviceId))
            .Select(a => a.ToApplicationInfo(deviceId))
            .OrderBy(a => a.AppName)
            .ToObservableCollection();
    }

    public async Task AddOrUpdateApplicationForDevice(ApplicationInfoEntity application, string deviceId)
    {
        var existingApp = context.Database.Find<ApplicationInfoEntity>(application.PackageName);
        if (existingApp != null)
        {
            // Update app info (icon, name might be different)
            existingApp.AppName = application.AppName;
            existingApp.AppIconPath = application.AppIconPath ?? existingApp.AppIconPath;
            
            // Add device to existing app if not already present
            if (!HasDevice(existingApp, deviceId))
            {
                var deviceInfoList = existingApp.AppDeviceInfoList;
                deviceInfoList.Add(new AppDeviceInfo(deviceId, NotificationFilter.ToastFeed));
                existingApp.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
            }
            
            context.Database.Update(existingApp);

            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                var appToUpdate = Applications.FirstOrDefault(a => a.PackageName == existingApp.PackageName);
                if (appToUpdate != null)
                {
                    var updatedAppInfo = existingApp.ToApplicationInfo(deviceId);
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
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                Applications.Add(application.ToApplicationInfo(deviceId));
            });
        }
    }

    public async Task<NotificationFilter> AddOrUpdateApplicationForDevice(string deviceId, string appPackage, string? appName = null, string? appIcon = null)
    {
        NotificationFilter filter = NotificationFilter.ToastFeed;
        var app = context.Database.Find<ApplicationInfoEntity>(appPackage);
        if (app != null)
        {
            var deviceInfoList = app.AppDeviceInfoList;
            var deviceInfo = deviceInfoList.FirstOrDefault(d => d.DeviceId == deviceId);
            // Add device to app if not already present
            if (deviceInfo == null)
            {
                deviceInfoList.Add(new AppDeviceInfo(deviceId, filter));
                app.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
            }
            else
            {
                deviceInfo.Filter = filter;
                app.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
            }
            context.Database.Update(app);
        }
        else
        {
            var newAppInfo = new ApplicationInfoMessage
            {
                PackageName = appPackage,
                AppName = appName ?? appPackage,
                AppIcon = appIcon ?? null,
            };

            var appEntity = await ApplicationInfoEntity.FromApplicationInfoMessage(newAppInfo, deviceId);
            context.Database.Insert(appEntity);
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => Applications.Add(appEntity.ToApplicationInfo(deviceId)));
        }
        return filter;
    }

    public NotificationFilter? GetAppNotificationFilterAsync(string appPackage, string deviceId)
    {
        var app = context.Database.Find<ApplicationInfoEntity>(appPackage);
        if (app != null && HasDevice(app, deviceId, out var deviceInfo))
        {
            return deviceInfo?.Filter;
        }
        return null;
    }

    public void UpdateAppNotificationFilter(string deviceId, string appPackage, NotificationFilter filter)
    {
        var app = context.Database.Find<ApplicationInfoEntity>(appPackage);
        var deviceInfoList = app.AppDeviceInfoList;
        deviceInfoList.First(d => d.DeviceId == deviceId).Filter = filter;
        app.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
        context.Database.Update(app);
    }

    public async Task RemoveDeviceFromApplication(string appPackage, string deviceId)
    {
        var app = context.Database.Find<ApplicationInfoEntity>(appPackage);
        if (app != null)
        {
            var deviceInfoList = app.AppDeviceInfoList;
            deviceInfoList.RemoveAll(d => d.DeviceId == deviceId);
            app.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
            
            if (deviceInfoList.Count == 0)
            {
                // No more devices have this app, delete it
                context.Database.Delete(app);
                await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                {
                    var appToRemove = Applications.FirstOrDefault(a => a.PackageName == appPackage);
                    if (appToRemove != null)
                    {
                        Applications.Remove(appToRemove);
                    }
                });
            }
            else
            {
                // Still has other devices, just update
                context.Database.Update(app);
                await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                {
                    var appToUpdate = Applications.FirstOrDefault(a => a.PackageName == appPackage);
                    if (appToUpdate != null)
                    {
                        var updatedAppInfo = app.ToApplicationInfo(deviceId);
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
            foreach (var appInfo in applicationList.AppList)
            {
                var appEntity = await ApplicationInfoEntity.FromApplicationInfoMessage(appInfo, pairedDevice.Id);
                await AddOrUpdateApplicationForDevice(appEntity, pairedDevice.Id);
            }

            await LoadApplicationsFromDevice(pairedDevice.Id);
            ApplicationListUpdated?.Invoke(this, pairedDevice.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating application list for device {DeviceId}", pairedDevice.Id);
        }
    }

    public void RemoveAllAppsForDeviceAsync(string deviceId)
    {
        var allApps = context.Database.Table<ApplicationInfoEntity>();
        List<ApplicationInfoEntity> appsToDelete = [];
        foreach (var app in allApps)
        {
            if (HasDevice(app, deviceId))
            {
                var deviceInfoList = app.AppDeviceInfoList;
                deviceInfoList.RemoveAll(d => d.DeviceId == deviceId);
                app.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
                
                if (deviceInfoList.Count == 0)
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

    public void PinApp(ApplicationInfo appInfo, string deviceId)
    {
        var app = context.Database.Find<ApplicationInfoEntity>(appInfo.PackageName);
        var deviceInfoList = app.AppDeviceInfoList;
        deviceInfoList.First(d => d.DeviceId == deviceId).Pinned = true;
        app.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
        context.Database.Update(app);
    }

    public void UnpinApp(ApplicationInfo appInfo, string deviceId)
    {
        var app = context.Database.Find<ApplicationInfoEntity>(appInfo.PackageName);
        var deviceInfoList = app.AppDeviceInfoList;
        deviceInfoList.First(d => d.DeviceId == deviceId).Pinned = false;
        app.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
        context.Database.Update(app);
    }

    public string? GetAppIcon(string packageName)
    {
        var app = context.Database.Find<ApplicationInfoEntity>(packageName);
        if (app != null)
        {
            return app.AppIconPath;
        }
        return null;
    }
    
    #region Helpers
    private static bool HasDevice(ApplicationInfoEntity entity, string deviceId)
    {
        return entity.AppDeviceInfoList.Any(d => d.DeviceId == deviceId);
    }

    private static bool HasDevice(ApplicationInfoEntity entity, string deviceId, out AppDeviceInfo? deviceInfo)
    {
        deviceInfo = null;
        
        if (string.IsNullOrEmpty(entity.AppDeviceInfoJson))
            return false;
            
        try
        {
            var deviceInfoList = entity.AppDeviceInfoList;
            deviceInfo = deviceInfoList.FirstOrDefault(d => d.DeviceId == deviceId);
            return deviceInfo != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    #endregion
}
