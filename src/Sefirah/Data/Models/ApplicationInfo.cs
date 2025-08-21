using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.Enums;
using Sefirah.Extensions;

namespace Sefirah.Data.Models;

public partial class ApplicationInfo : ObservableObject
{
    private string packageName = string.Empty;
    public string PackageName 
    { 
        get => packageName;
        set => SetProperty(ref packageName, value);
    }
    
    private string appName = string.Empty;
    public string AppName 
    { 
        get => appName;
        set => SetProperty(ref appName, value);
    }
    
    private string? iconPath;
    public string? IconPath
    {
        get => iconPath;
        set => SetProperty(ref iconPath, value);
    }
    
    private List<AppDeviceInfo> deviceInfo = [];
    public List<AppDeviceInfo> DeviceInfo 
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

    public string currentNotificationFilter = string.Empty;
    public string CurrentNotificationFilter
    {
        get => currentNotificationFilter;
        set
        {
            currentNotificationFilter = value;
            OnPropertyChanged(nameof(CurrentNotificationFilter));
        }
    }

    #region Helpers
    public void AddDevice(string deviceId)
    {
        if (!DeviceInfo.Any(d => d.DeviceId == deviceId))
        {
            var newDeviceInfo = new List<AppDeviceInfo>(DeviceInfo)
            {
                new() { DeviceId = deviceId, Filter = NotificationFilter.ToastFeed }
            };
            DeviceInfo = newDeviceInfo;
        }
    }

    public void RemoveDevice(string deviceId)
    {
        var deviceToRemove = DeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
        if (deviceToRemove is not null)
        {
            var newDeviceInfo = new List<AppDeviceInfo>(DeviceInfo);
            newDeviceInfo.Remove(deviceToRemove);
            DeviceInfo = newDeviceInfo;
        }
    }

    public bool HasDevice(string deviceId)
    {
        return DeviceInfo.Any(d => d.DeviceId == deviceId);
    }

    public bool IsPinned(string deviceId)
    {
        var deviceInfo = DeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
        return deviceInfo?.Pinned ?? false;
    }

    public void SetPinned(string deviceId, bool pinned)
    {
        var deviceInfo = DeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
        if (deviceInfo != null)
        {
            deviceInfo.Pinned = pinned;
            OnPropertyChanged(nameof(DeviceInfo));
        }
    }

    public void UpdateNotificationFilter(string deviceId)
    {
        CurrentNotificationFilter = GetNotificationFilter(deviceId);
    }

    public string GetNotificationFilter(string deviceId)
    {
        var deviceInfo = DeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
        if (deviceInfo != null)
        {
            return NotificationFilterTypes[deviceInfo.Filter];
        }
        return NotificationFilterTypes[NotificationFilter.ToastFeed];
    }

    public static Dictionary<NotificationFilter, string> NotificationFilterTypes { get; } = new()
    {
        { NotificationFilter.ToastFeed, "NotificationFilterToastFeed.Content".GetLocalizedResource() },
        { NotificationFilter.Feed, "NotificationFilterFeed.Content".GetLocalizedResource() },
        { NotificationFilter.Disabled, "NotificationFilterDisabled.Content".GetLocalizedResource() }
    };

    #endregion
}
