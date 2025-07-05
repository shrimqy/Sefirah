using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using SQLite;
using Sefirah.Extensions;

namespace Sefirah.Data.AppDatabase.Models;

public partial class ApplicationInfoEntity : ObservableObject
{
    [PrimaryKey]
    public string AppPackage { get; set; } = string.Empty;
    
    public string AppName { get; set; } = string.Empty;
    
    private byte[]? _appIconBytes;
    public byte[]? AppIconBytes 
    { 
        get => _appIconBytes;
        set 
        {
            if (SetProperty(ref _appIconBytes, value))
            {
                _appIcon = null;
                OnPropertyChanged(nameof(AppIcon));
            }
        }
    }

    private BitmapImage? _appIcon;

    [NotMapped]
    [Ignore]
    public BitmapImage? AppIcon
    {
        get 
        {
            if (_appIcon == null && _appIconBytes != null)
            {
                _appIcon = _appIconBytes.ToBitmap();
            }
            return _appIcon;
        }
        set => SetProperty(ref _appIcon, value);
    }
    [SQLite.Column("AppDeviceInfo")]
    public string? AppDeviceInfoJson { get; set; }

    private List<AppDeviceInfo>? _appDeviceInfo;

    [Ignore]
    public List<AppDeviceInfo> AppDeviceInfo
    {
        get
        {
            if (_appDeviceInfo is null)
            {
                if (string.IsNullOrEmpty(AppDeviceInfoJson))
                {
                    _appDeviceInfo = [];
                }
                else
                {
                    try
                    {
                        _appDeviceInfo = JsonSerializer.Deserialize<List<AppDeviceInfo>>(AppDeviceInfoJson) ?? [];
                    }
                    catch (JsonException)
                    {
                        // Handle cases where JSON might be invalid
                        _appDeviceInfo = [];
                    }
                }
            }
            return _appDeviceInfo;
        }
        set
        {
            _appDeviceInfo = value;
            AppDeviceInfoJson = JsonSerializer.Serialize(value);
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set 
        {
            SetProperty(ref _isLoading, value);
        }
    }

    public void AddDevice(string deviceId)
    {
        var devices = new List<AppDeviceInfo>(AppDeviceInfo);
        if (!devices.Any(d => d.DeviceId == deviceId))
        {
            devices.Add(new AppDeviceInfo { DeviceId = deviceId, Filter = NotificationFilter.ToastFeed });
            AppDeviceInfo = devices;
        }
    }

    public void RemoveDevice(string deviceId)
    {
        var devices = new List<AppDeviceInfo>(AppDeviceInfo);
        var deviceToRemove = devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (deviceToRemove is not null)
        {
            devices.Remove(deviceToRemove);
            AppDeviceInfo = devices;
        }
    }

    public bool HasDevice(string deviceId)
    {
        return AppDeviceInfo.Any(d => d.DeviceId == deviceId);
    }

    public bool IsPinned(string deviceId)
    {
        var deviceInfo = AppDeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
        return deviceInfo?.Pinned ?? false;
    }

    public void SetPinned(string deviceId, bool pinned)
    {
        var devices = new List<AppDeviceInfo>(AppDeviceInfo);
        var deviceInfo = devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (deviceInfo != null)
        {
            deviceInfo.Pinned = pinned;
            AppDeviceInfo = devices;
        }
    }

    public static ApplicationInfoEntity FromApplicationInfo(ApplicationInfo info, string deviceId)
    {
        return new ApplicationInfoEntity
        {
            AppPackage = info.PackageName,
            AppName = info.AppName,
            AppIconBytes = !string.IsNullOrEmpty(info.AppIcon) 
                ? Convert.FromBase64String(info.AppIcon) 
                : null, 
            AppDeviceInfo = [new AppDeviceInfo { DeviceId = deviceId, Filter = NotificationFilter.ToastFeed }],
        };
    }

    public static Dictionary<NotificationFilter, string> NotificationFilterTypes { get; } = new()
    {
        { NotificationFilter.ToastFeed, "NotificationFilterToastFeed.Content".GetLocalizedResource() },
        { NotificationFilter.Feed, "NotificationFilterFeed.Content".GetLocalizedResource() },
        { NotificationFilter.Disabled, "NotificationFilterDisabled.Content".GetLocalizedResource() }
    };

    public string GetNotificationFilter(string deviceId)
    {
        var deviceInfo = AppDeviceInfo.FirstOrDefault(d => d.DeviceId == deviceId);
        if (deviceInfo != null)
        {
            return NotificationFilterTypes[deviceInfo.Filter];
        }
        // Return default if device not found
        return NotificationFilterTypes[NotificationFilter.ToastFeed];
    }

    public string currentNotificationFilter;
    public string CurrentNotificationFilter
    {
        get => currentNotificationFilter;
        set
        {
            currentNotificationFilter = value;
            OnPropertyChanged(nameof(CurrentNotificationFilter));
        }
    }

    public void UpdateNotificationFilter(string deviceId)
    {
        CurrentNotificationFilter = GetNotificationFilter(deviceId);
    }
}

public class AppDeviceInfo
{
    public string DeviceId { get; set; }
    public NotificationFilter Filter { get; set; }
    public bool Pinned { get; set; } = false;
}
