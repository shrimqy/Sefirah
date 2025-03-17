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
    /// Gets or sets a value indicating whether to ignore notifications during DND.
    /// </summary>
    bool IgnoreNotificationDuringDnd { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to copy received files to clipboard.
    /// </summary>
    bool ClipboardFilesEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include images in clipboard sync.
    /// </summary>
    bool ImageToClipboardEnabled { get; set; }

    /// <summary>
    /// Gets or sets the path to the scrcpy executable.
    /// </summary>
    string? ScrcpyPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to turn off the screen.
    /// </summary>
    bool ScreenOff { get; set; }

    /// <summary>   
    /// Gets or sets a value indicating whether to use a physical keyboard.
    /// </summary>
    bool PhysicalKeyboard { get; set; }

    /// <summary>
    /// Gets or sets the video bitrate.
    /// </summary>
    string? VideoBitrate { get; set; }  

    /// <summary>
    /// Gets or sets the video resolution.
    /// </summary>
    string? VideoResolution { get; set; }

    /// <summary>
    /// Gets or sets the video buffer.  
    /// </summary>
    string? VideoBuffer { get; set; }

    /// <summary>
    /// Gets or sets the audio bitrate.
    /// </summary>
    string? AudioBitrate { get; set; }  

    /// <summary>
    /// Gets or sets the audio buffer.
    /// </summary>
    string? AudioBuffer { get; set; }
}
