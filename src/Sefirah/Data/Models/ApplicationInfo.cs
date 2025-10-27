using Sefirah.Data.Enums;
using Sefirah.Extensions;

namespace Sefirah.Data.Models;

public partial class ApplicationInfo(string packageName, string appName, string? iconPath, AppDeviceInfo deviceInfo) : ObservableObject
{
    private string packageName = packageName;
    public string PackageName 
    { 
        get => packageName;
        set => SetProperty(ref packageName, value);
    }
    
    private string appName = appName;
    public string AppName 
    { 
        get => appName;
        set => SetProperty(ref appName, value);
    }
    
    private string? iconPath = iconPath;
    public string? IconPath
    {
        get => iconPath;
        set => SetProperty(ref iconPath, value);
    }
    
    private AppDeviceInfo deviceInfo = deviceInfo;
    public AppDeviceInfo DeviceInfo 
    { 
        get => deviceInfo;
        set => SetProperty(ref deviceInfo, value);
    }
    
    private bool isLoading;
    public bool IsLoading
    {
        get => isLoading;
        set => SetProperty(ref isLoading, value);
    }

    #region Helpers
    private string? selectedNotificationFilter;
    public string SelectedNotificationFilter
    {
        get => selectedNotificationFilter ?? NotificationFilterTypes[DeviceInfo.Filter];
        set => SetProperty(ref selectedNotificationFilter, value);
    }

    public static Dictionary<NotificationFilter, string> NotificationFilterTypes { get; } = new()
    {
        { NotificationFilter.ToastFeed, "NotificationFilterToastFeed.Content".GetLocalizedResource() },
        { NotificationFilter.Feed, "NotificationFilterFeed.Content".GetLocalizedResource() },
        { NotificationFilter.Disabled, "NotificationFilterDisabled.Content".GetLocalizedResource() }
    };

    #endregion
}
