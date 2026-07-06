using Sefirah.Data.Models;
using Sefirah.Utils;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class ApplicationEntity
{
    [PrimaryKey]
    public string AppKey { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    [Indexed]
    public string PackageName { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public bool Pinned { get; set; }

    public NotificationFilter Filter { get; set; } = NotificationFilter.ToastFeed;

    #region Helpers

    public static string GetKey(string deviceId, string packageName) => $"{deviceId}:{packageName}";

    public static async Task<ApplicationEntity> FromApplicationInfo(
        ApplicationInfo info,
        string deviceId,
        bool pinned = false,
        NotificationFilter filter = NotificationFilter.ToastFeed)
    {
        await IconUtils.SaveAppIconToPathAsync(info.AppIcon, deviceId, info.PackageName);

        return new ApplicationEntity
        {
            AppKey = GetKey(deviceId, info.PackageName),
            DeviceId = deviceId,
            PackageName = info.PackageName,
            AppName = info.AppName,
            Pinned = pinned,
            Filter = filter,
        };
    }

    internal ApplicationItem ToApplicationItem() =>
        new(DeviceId, PackageName, AppName, LocalAppPaths.GetAppIconPath(DeviceId, PackageName))
        {
            Pinned = Pinned,
            Filter = Filter,
        };

    #endregion
}
