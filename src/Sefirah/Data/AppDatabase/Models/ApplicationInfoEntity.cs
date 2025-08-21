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
    public string? AppDeviceInfoJson { get; set; }

    #region Helpers
    internal ApplicationInfo ToApplicationInfo()
    {
        List<AppDeviceInfo> deviceInfo = [];
        if (!string.IsNullOrEmpty(AppDeviceInfoJson))
        {
            deviceInfo = JsonSerializer.Deserialize<List<AppDeviceInfo>>(AppDeviceInfoJson) ?? [];
        }

        return new ApplicationInfo
        {
            PackageName = PackageName,
            AppName = AppName,
            IconPath = AppIconPath,
            DeviceInfo = deviceInfo
        };
    }

    internal static ApplicationInfoEntity FromApplicationInfo(ApplicationInfo info, string deviceId)
    {
        List<AppDeviceInfo> appDeviceInfo = [new AppDeviceInfo { DeviceId = deviceId, Filter = NotificationFilter.ToastFeed }];
        return new ApplicationInfoEntity
        {
            PackageName = info.PackageName,
            AppName = info.AppName,
            AppIconPath = info.IconPath,
            AppDeviceInfoJson = JsonSerializer.Serialize(appDeviceInfo)
        };  
    }

    internal static async Task<ApplicationInfoEntity> FromApplicationInfoMessage(ApplicationInfoMessage info, string deviceId)
    {
        string? appIconPath = null;
        if (!string.IsNullOrEmpty(info.AppIcon))
        {
            try
            {
                var iconBytes = Convert.FromBase64String(info.AppIcon);
                var fileName = $"{info.PackageName}.png";
                appIconPath = await ImageUtils.SaveAppIconToPathAsync(iconBytes, fileName);
            }
            catch (Exception) { }
        }

        List<AppDeviceInfo> appDeviceInfo = [new AppDeviceInfo { DeviceId = deviceId, Filter = NotificationFilter.ToastFeed }];

        return new ApplicationInfoEntity
        {
            PackageName = info.PackageName,
            AppName = info.AppName,
            AppIconPath = appIconPath,
            AppDeviceInfoJson = JsonSerializer.Serialize(appDeviceInfo)
        };
    }
    #endregion
}
