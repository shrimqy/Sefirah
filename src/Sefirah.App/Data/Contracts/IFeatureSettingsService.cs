using Sefirah.App.Data.Enums;

namespace Sefirah.App.Data.Contracts;

public interface IFeatureSettingsService : IBaseSettingsService, INotifyPropertyChanged
{
    /// <summary>
    /// Gets or sets a value indicating whether clipboard synchronization is enabled.
    /// </summary>
    bool ClipboardSyncEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show clipboard toast notifications.
    /// </summary>
    bool ShowClipboardToast { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether links should be opened in the browser.
    /// </summary>
    bool OpenLinksInBrowser { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether notification synchronization is enabled.
    /// </summary>
    bool NotificationSyncEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show notification toast messages.
    /// </summary>
    bool ShowNotificationToast { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show badge for notifications on taskbar.
    /// </summary>
    bool ShowBadge { get; set; }

    /// <summary>
    /// Gets or sets the preferred launch behavior for notifications.
    /// </summary>
    NotificationLaunchPreference NotificationLaunchPreference { get; set; }

    /// <summary>
    /// Gets or sets the path for remote storage.
    /// </summary>
    string RemoteStoragePath { get; set; }

    /// <summary>
    /// Gets or sets the path for received files.
    /// </summary>
    string ReceivedFilesPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore notifications for apps already installed on Windows.
    /// </summary>
    bool IgnoreWindowsApps { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to copy received files to clipboard.
    /// </summary>
    bool ClipboardFilesEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include images in clipboard sync.
    /// </summary>
    bool ImageToClipboardEnabled { get; set; }
}
