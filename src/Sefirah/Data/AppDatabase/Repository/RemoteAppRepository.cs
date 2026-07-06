using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Data.AppDatabase.Repository;

public class RemoteAppRepository(DatabaseContext context, ILogger logger)
{
    public event EventHandler<string>? ApplicationListUpdated;
    public event EventHandler<(string deviceId, ApplicationItem? appInfo, string? packageName)>? ApplicationItemUpdated;

    public IEnumerable<ApplicationItem> GetApplicationsForDevice(string deviceId) =>
        context.Database.Table<ApplicationEntity>()
            .Where(a => a.DeviceId == deviceId)
            .OrderBy(a => a.AppName)
            .Select(a => a.ToApplicationItem());

    public ApplicationItem? GetApplicationForDevice(string deviceId, string packageName)
    {
        var entity = context.Database.Find<ApplicationEntity>(
            ApplicationEntity.GetKey(deviceId, packageName));

        return entity?.ToApplicationItem();
    }

    public async Task AddOrUpdateApplicationForDevice(ApplicationInfo application, string deviceId)
    {
        var appKey = ApplicationEntity.GetKey(deviceId, application.PackageName);
        var existing = context.Database.Find<ApplicationEntity>(appKey);
        var entity = await ApplicationEntity.FromApplicationInfo(
            application,
            deviceId,
            existing?.Pinned ?? false,
            existing?.Filter ?? NotificationFilter.ToastFeed);

        context.Database.InsertOrReplace(entity);
        ApplicationItemUpdated?.Invoke(this, (deviceId, entity.ToApplicationItem(), null));
    }

    public async Task<NotificationFilter> GetOrCreateAppNotificationFilter(
        string deviceId,
        string appPackage,
        string appName,
        string? appIcon = null)
    {
        var exisiting = context.Database.Find<ApplicationEntity>(ApplicationEntity.GetKey(deviceId, appPackage));

        if (exisiting is not null)
            return exisiting.Filter;

        var newAppInfo = new ApplicationInfo
        {
            PackageName = appPackage,
            AppName = appName,
            AppIcon = appIcon,
        };

        var entity = await ApplicationEntity.FromApplicationInfo(newAppInfo, deviceId);
        context.Database.InsertOrReplace(entity);
        ApplicationItemUpdated?.Invoke(this, (deviceId, entity.ToApplicationItem(), null));

        return NotificationFilter.ToastFeed;
    }

    public void UpdateAppNotificationFilter(string deviceId, string appPackage, NotificationFilter filter)
    {
        var entity = context.Database.Find<ApplicationEntity>(
            ApplicationEntity.GetKey(deviceId, appPackage));
        if (entity is null)
            return;

        entity.Filter = filter;
        context.Database.Update(entity);
    }

    public void UpdateAllAppNotificationFilters(string deviceId, NotificationFilter filter)
    {
        var apps = context.Database.Table<ApplicationEntity>()
            .Where(a => a.DeviceId == deviceId)
            .ToList();

        if (apps.Count == 0)
            return;

        context.Database.RunInTransaction(() =>
        {
            foreach (var app in apps)
            {
                app.Filter = filter;
                context.Database.Update(app);
            }
        });
    }

    public Task RemoveDeviceFromApplication(string appPackage, string deviceId)
    {
        var appKey = ApplicationEntity.GetKey(deviceId, appPackage);
        if (context.Database.Delete<ApplicationEntity>(appKey) > 0)
        {
            IconUtils.DeleteAppIcon(deviceId, appPackage);
            ApplicationItemUpdated?.Invoke(this, (deviceId, null, appPackage));
        }

        return Task.CompletedTask;
    }

    public async Task UpdateApplicationList(PairedDevice pairedDevice, ApplicationList applicationList)
    {
        try
        {
            var currentPackageNames = context.Database.Table<ApplicationEntity>()
                .Where(a => a.DeviceId == pairedDevice.Id)
                .Select(a => a.PackageName)
                .ToHashSet();

            var newPackageNames = applicationList.AppList.Select(a => a.PackageName).ToHashSet();

            foreach (var packageName in currentPackageNames.Except(newPackageNames))
                await RemoveDeviceFromApplication(packageName, pairedDevice.Id);

            foreach (var appInfo in applicationList.AppList)
                await AddOrUpdateApplicationForDevice(appInfo, pairedDevice.Id);

            ApplicationListUpdated?.Invoke(this, pairedDevice.Id);
        }
        catch (Exception ex)
        {
            logger.Error($"Error updating application list for device {pairedDevice.Id}", ex);
        }
    }

    public void RemoveAllAppsForDevice(string deviceId)
    {
        context.Database.Table<ApplicationEntity>()
            .Where(a => a.DeviceId == deviceId)
            .Delete();

        LocalAppPaths.DeleteDeviceIcons(deviceId);
        ApplicationListUpdated?.Invoke(this, deviceId);
    }

    public void PinApp(ApplicationItem appInfo, string deviceId)
    {
        var entity = context.Database.Find<ApplicationEntity>(
            ApplicationEntity.GetKey(deviceId, appInfo.PackageName));
        if (entity is null)
            return;

        entity.Pinned = true;
        context.Database.Update(entity);
    }

    public void UnpinApp(ApplicationItem appInfo, string deviceId)
    {
        var entity = context.Database.Find<ApplicationEntity>(
            ApplicationEntity.GetKey(deviceId, appInfo.PackageName));
        if (entity is null)
            return;

        entity.Pinned = false;
        context.Database.Update(entity);
    }
}
