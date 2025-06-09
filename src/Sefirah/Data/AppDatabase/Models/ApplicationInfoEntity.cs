using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using SQLite;
using System.Text.Json;

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
                // Clear the cached AppIcon so it gets regenerated
                _appIcon = null;
                OnPropertyChanged(nameof(AppIcon));
            }
        }
    }
    
    // Store device IDs as JSON string
    public string DeviceIdsJson { get; set; } = "[]";
    
    [Ignore]
    public List<string> DeviceIds
    {
        get => string.IsNullOrEmpty(DeviceIdsJson) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(DeviceIdsJson) ?? new List<string>();
        set 
        {
            DeviceIdsJson = JsonSerializer.Serialize(value);
            OnPropertyChanged();
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

    private NotificationFilter _notificationFilter;
    public NotificationFilter NotificationFilter
    {
        get => _notificationFilter;
        set => SetProperty(ref _notificationFilter, value);
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
        var devices = DeviceIds;
        if (!devices.Contains(deviceId))
        {
            devices.Add(deviceId);
            DeviceIds = devices;
        }
    }

    public void RemoveDevice(string deviceId)
    {
        var devices = DeviceIds;
        if (devices.Remove(deviceId))
        {
            DeviceIds = devices;
        }
    }

    public bool HasDevice(string deviceId)
    {
        return DeviceIds.Contains(deviceId);
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
            NotificationFilter = NotificationFilter.ToastFeed,
            DeviceIds = [deviceId]
        };
    }
}
