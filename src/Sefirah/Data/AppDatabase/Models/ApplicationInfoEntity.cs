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

    public string? AppIconPath { get; set; }

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
        return new ApplicationInfo(PackageName, AppName, AppIconPath, deviceInfo);
    }

    internal static async Task<ApplicationInfoEntity> FromApplicationInfoMessage(ApplicationInfoMessage info, string deviceId)
    {
        List<AppDeviceInfo> appDeviceInfoList = [new(deviceId, NotificationFilter.ToastFeed)];

        return new ApplicationInfoEntity
        {
            PackageName = info.PackageName,
            AppName = info.AppName,
            AppIconPath = await IconUtils.SaveAppIconToPathAsync(info.AppIcon, info.PackageName),
            AppDeviceInfoJson = JsonSerializer.Serialize(appDeviceInfoList)
        };
    }
    #endregion
}
