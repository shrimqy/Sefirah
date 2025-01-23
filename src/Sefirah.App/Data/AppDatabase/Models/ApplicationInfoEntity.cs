using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sefirah.App.Data.AppDatabase.Models;
public class ApplicationInfoEntity : BaseEntity
{
    public string AppPackage { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public byte[]? AppIconBytes { get; set; }

    private BitmapImage? _appIcon;
    [NotMapped]
    public BitmapImage? AppIcon
    {
        get => _appIcon;
        set => Set(ref _appIcon, value);
    }

    private NotificationFilter _notificationFilter;
    public NotificationFilter NotificationFilter
    {
        get => _notificationFilter;
        set => Set(ref _notificationFilter, value);
    }

    public static ApplicationInfoEntity FromApplicationInfo(ApplicationInfo info)
    {
        return new ApplicationInfoEntity
        {
            AppPackage = info.PackageName,
            AppName = info.AppName,
            AppIconBytes = !string.IsNullOrEmpty(info.AppIcon) 
                ? Convert.FromBase64String(info.AppIcon) 
                : null,
            NotificationFilter = NotificationFilter.ToastFeed
        };
    }
}
