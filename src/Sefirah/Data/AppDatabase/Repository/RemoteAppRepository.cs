using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;
public class RemoteAppRepository(DatabaseContext context, ILogger<RemoteAppRepository> logger)
{
    public ObservableCollection<ApplicationInfoEntity> Applications { get; set; } = [];
    
    // Event to notify when application list update is complete
    public event EventHandler<string>? ApplicationListUpdated;

    public void LoadApplicationsFromDevice(string deviceId)
    {
        App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
        {
            Applications.Clear();
            
            var appsForDevice = context.Database.Table<ApplicationInfoEntity>()
                .ToList()
                .Where(a => a.HasDevice(deviceId))
                .OrderBy(a => a.AppName)
                .ToList();

            foreach (var app in appsForDevice)
            {
                Applications.Add(app);
            }
        });
    }

    public List<ApplicationInfoEntity> GetApplicationsFromDevice(string deviceId)
    {
        var appsForDevice = context.Database.Table<ApplicationInfoEntity>()
            .ToList()
            .Where(a => a.HasDevice(deviceId))
            .OrderBy(a => a.AppName)
            .ToList();
        return appsForDevice;
    }

    public void AddOrUpdateApplication(ApplicationInfoEntity application)
    {
        var existingApp = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.AppPackage == application.AppPackage);

        if (existingApp != null)
        {
            // Update app info (icon, name might be different)
            existingApp.AppName = application.AppName;
            existingApp.AppIconBytes = application.AppIconBytes ?? existingApp.AppIconBytes;
            existingApp.AddDevice(application.AppDeviceInfo.First().DeviceId);
            context.Database.Update(existingApp);

            App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
            {
                var appToUpdate = Applications.FirstOrDefault(a => a.AppPackage == existingApp.AppPackage);
                if (appToUpdate != null)
                {
                    appToUpdate.AppName = existingApp.AppName;
                    appToUpdate.AppIconBytes = existingApp.AppIconBytes;
                    appToUpdate.AddDevice(application.AppDeviceInfo.First().DeviceId);
                }
            });
        }
        else
        {
            context.Database.Insert(application);
            App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
            {
                Applications.Add(application);
            });
        }
    }

    public NotificationFilter? GetAppNotificationFilter(string appPackage, string deviceId)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.AppPackage == appPackage);
        
        // Check if this app is associated with the device
        if (app != null && app.HasDevice(deviceId))
        {
            return app.AppDeviceInfo.First(d => d.DeviceId == deviceId).Filter;
        }
        return null;
    }

    public NotificationFilter AddOrUpdateAppNotificationFilter(string deviceId, string appPackage, string? appName = null, byte[]? appIcon = null, NotificationFilter filter = NotificationFilter.ToastFeed)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.AppPackage == appPackage);

        if (app != null)
        {
            // Add device if not already added and update filter
            app.AddDevice(deviceId);
            var deviceInfo = new List<AppDeviceInfo>(app.AppDeviceInfo);
            var targetDevice = deviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
            if (targetDevice != null)
            {
                targetDevice.Filter = filter;
                app.AppDeviceInfo = deviceInfo;
            }
            context.Database.Update(app);

            App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
            {
                var appToUpdate = Applications.FirstOrDefault(a => a.AppPackage == appPackage);
                if (appToUpdate != null)
                {
                    appToUpdate.AddDevice(deviceId);
                }
            });
        }
        else
        {
            var newApp = new ApplicationInfoEntity
            {
                AppPackage = appPackage,
                AppName = appName,
                AppIconBytes = appIcon,
                AppDeviceInfo = [new AppDeviceInfo { DeviceId = deviceId, Filter = filter }]
            };
            newApp.AddDevice(deviceId);
            context.Database.Insert(newApp);
            App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
            {
                Applications.Add(newApp);
            });
        }
        return filter;
    }


    public void UpdateAppNotificationFilter(string deviceId, string appPackage, NotificationFilter filter)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.AppPackage == appPackage);
        
        if (app != null)
        {
            var deviceInfo = new List<AppDeviceInfo>(app.AppDeviceInfo);
            var targetDevice = deviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
            if (targetDevice != null)
            {
                targetDevice.Filter = filter;
                app.AppDeviceInfo = deviceInfo;
            }
            
            context.Database.Update(app);
        }
    }

    public void RemoveDeviceFromApplication(string appPackage, string deviceId)
    {
        var app = context.Database.Table<ApplicationInfoEntity>()
            .FirstOrDefault(a => a.AppPackage == appPackage);

        if (app != null)
        {
            app.RemoveDevice(deviceId);
            
            if (app.AppDeviceInfo.Count == 0)
            {
                // No more devices have this app, delete it
                context.Database.Delete(app);
                App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
                {
                    var appToRemove = Applications.FirstOrDefault(a => a.AppPackage == appPackage);
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
                App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
                {
                    var appToUpdate = Applications.FirstOrDefault(a => a.AppPackage == appPackage);
                    appToUpdate?.RemoveDevice(deviceId);
                });
            }
        }
    }

    public void UpdateApplicationList(PairedDevice pairedDevice, ApplicationList applicationList)
    {
        RemoveAllAppsForDevice(pairedDevice.Id);

        // Add/update apps from the new list
        foreach (var appInfo in applicationList.AppList)
        {
            var appEntity = ApplicationInfoEntity.FromApplicationInfo(appInfo, pairedDevice.Id);
            AddOrUpdateApplication(appEntity);
        }

        // Reload applications for this device
        LoadApplicationsFromDevice(pairedDevice.Id);

        // Notify that the update is complete
        ApplicationListUpdated?.Invoke(this, pairedDevice.Id);
    }

    public void RemoveAllAppsForDevice(string deviceId)
    {
        var allApps = context.Database.Table<ApplicationInfoEntity>().ToList();
        var appsToDelete = new List<ApplicationInfoEntity>();
        
        foreach (var app in allApps)
        {
            if (app.HasDevice(deviceId))
            {
                app.RemoveDevice(deviceId);
                if (app.AppDeviceInfo.Count == 0)
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
            .Where(a => a.HasDevice(deviceId))
            .ToList();
    }
}
