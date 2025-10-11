using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public partial class ApplicationInfoEntity
{
    [PrimaryKey]
    public string PackageName { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    [Column("AppDeviceInfo")]
    public string AppDeviceInfoJson { get; set; } = string.Empty;

    [Ignore]
    public List<AppDeviceInfo> AppDeviceInfoList
    {
        get => JsonSerializer.Deserialize<List<AppDeviceInfo>>(AppDeviceInfoJson) ?? [];
        set => AppDeviceInfoJson = JsonSerializer.Serialize(value);
    }

    #region Helpers
    internal ApplicationInfo ToApplicationInfo(string deviceId)
    {
        var deviceInfo =  AppDeviceInfoList.FirstOrDefault(d => d.DeviceId == deviceId) ?? new AppDeviceInfo(deviceId, NotificationFilter.ToastFeed);
        return new ApplicationInfo(PackageName, AppName, IconUtils.GetAppIconPath(PackageName), deviceInfo);
    }

    internal static async Task<ApplicationInfoEntity> FromApplicationInfoMessage(ApplicationInfoMessage info, string deviceId)
    {
        List<AppDeviceInfo> appDeviceInfoList = [new(deviceId, NotificationFilter.ToastFeed)];
        IconUtils.SaveAppIconToPathAsync(info.AppIcon, info.PackageName);
        return new ApplicationInfoEntity
        {
            PackageName = info.PackageName,
            AppName = info.AppName,
            AppDeviceInfoJson = JsonSerializer.Serialize(appDeviceInfoList)
        };
    }
    #endregion
}
