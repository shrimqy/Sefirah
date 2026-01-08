using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Data.AppDatabase.Repository;

public class RemoteAppRepository(DatabaseContext context, ILogger logger)
{
    public event EventHandler<string>? ApplicationListUpdated;
    public event EventHandler<(string deviceId, ApplicationInfo? appInfo, string? packageName)>? ApplicationItemUpdated;

    public ObservableCollection<ApplicationInfo> GetApplicationsForDevice(string deviceId)
    {
        return context.Database.Table<ApplicationInfoEntity>()
            .ToList()
            .Where(a => HasDevice(a, deviceId))
            .Select(a => a.ToApplicationInfo(deviceId))
            .OrderBy(a => a.AppName)
            .ToObservableCollection();
    }

    public async Task AddOrUpdateApplicationForDevice(ApplicationInfoMessage application, string deviceId)
    {
        ApplicationInfo appInfo;
        var existingApp = context.Database.Find<ApplicationInfoEntity>(application.PackageName);        
        if (existingApp is not null)
        {
            await IconUtils.SaveAppIconToPathAsync(application.AppIcon, application.PackageName);

            // Add device to existing app if not already present
            if (!HasDevice(existingApp, deviceId))
            {
                var deviceInfoList = existingApp.AppDeviceInfoList;
                deviceInfoList.Add(new AppDeviceInfo(deviceId, NotificationFilter.ToastFeed));
                existingApp.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
            }
            
            context.Database.Update(existingApp);
            appInfo = existingApp.ToApplicationInfo(deviceId);
        }
        else
        {
            var applicationEntity = await ApplicationInfoEntity.FromApplicationInfoMessage(application, deviceId);
            context.Database.Insert(applicationEntity);
            appInfo = applicationEntity.ToApplicationInfo(deviceId);
        }
        
        ApplicationItemUpdated?.Invoke(this, (deviceId, appInfo, null));
    }

    public async Task<NotificationFilter> GetOrCreateAppNotificationFilter(string deviceId, string appPackage, string appName, string? appIcon = null)
    {
        var app = context.Database.Find<ApplicationInfoEntity>(appPackage);
        if (app is not null && HasDevice(app, deviceId, out var deviceInfo))
        {
            return deviceInfo?.Filter ?? NotificationFilter.ToastFeed;
        }
        
        // App doesn't exist or device not associated
        var newAppInfo = new ApplicationInfoMessage
        {
            PackageName = appPackage,
            AppName = appName,
            AppIcon = appIcon
        };

        await AddOrUpdateApplicationForDevice(newAppInfo, deviceId);
        return NotificationFilter.ToastFeed;
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
        if (app is not null)
        {
            var deviceInfoList = app.AppDeviceInfoList;
            deviceInfoList.RemoveAll(d => d.DeviceId == deviceId);
            app.AppDeviceInfoJson = JsonSerializer.Serialize(deviceInfoList);
            
            if (deviceInfoList.Count == 0)
            {
                context.Database.Delete(app);
                IconUtils.DeleteAppIcon(appPackage);
            }
            else
            {
                context.Database.Update(app);
            }
            
            // Fire event with null appInfo and packageName to signal removal
            ApplicationItemUpdated?.Invoke(this, (deviceId, null, appPackage));
        }
    }

    public async Task UpdateApplicationList(PairedDevice pairedDevice, ApplicationList applicationList)
    {
        try
        {
            // Remove all apps for this device from the database before adding the new list
            RemoveAllAppsForDeviceAsync(pairedDevice.Id);

            foreach (var appInfo in applicationList.AppList)
            {
                await AddOrUpdateApplicationForDevice(appInfo, pairedDevice.Id);
            }

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
            IconUtils.DeleteAppIcon(app.PackageName);
        }

        ApplicationListUpdated?.Invoke(this, deviceId);
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
