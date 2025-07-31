using Sefirah.Data.Enums;

namespace Sefirah.Data.Contracts;

public interface IDeviceSettingsService : IBaseSettingsService, INotifyPropertyChanged
{
    /// <summary>
    /// Gets the device ID this settings service is managing
    /// </summary>
    string DeviceId { get; }

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
    /// Gets or sets a value indicating whether to unlock the device before launching scrcpy.
    /// </summary>
    bool UnlockDeviceBeforeLaunch { get; set; }

    /// <summary>
    /// Gets or sets the unlock timeout.
    /// </summary>
    int UnlockTimeout { get; set; }

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
    
    /// <summary>
    /// Gets or sets custom command-line arguments for scrcpy.
    /// </summary>
    string? CustomArguments { get; set; }

    /// <summary>
    /// Gets or sets the unlock commands.
    /// </summary>
    string? UnlockCommands { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to disable video forwarding.
    /// </summary>
    bool DisableVideoForwarding { get; set; }

    /// <summary>
    /// Gets or sets the video codec.
    /// </summary>
    int VideoCodec { get; set; }

    /// <summary>
    /// Gets or sets the frame rate.
    /// </summary>
    string? FrameRate { get; set; }

    /// <summary>
    /// Gets or sets the crop settings.
    /// </summary>
    string? Crop { get; set; }

    /// <summary>
    /// Gets or sets the display number.
    /// </summary>
    string? Display { get; set; }

    /// <summary>
    /// Gets or sets the virtual display size.
    /// </summary>
    string? VirtualDisplaySize { get; set; }

    /// <summary>
    /// Gets or sets the display orientation.
    /// </summary>
    int DisplayOrientation { get; set; }

    /// <summary>
    /// Gets or sets the rotation angle.
    /// </summary>
    string? RotationAngle { get; set; }

    /// <summary>
    /// Gets or sets the audio output mode.
    /// </summary>
    AudioOutputModeType AudioOutputMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to forward microphone.
    /// </summary>
    bool ForwardMicrophone { get; set; }

    /// <summary>
    /// Gets or sets the audio output buffer.
    /// </summary>
    string? AudioOutputBuffer { get; set; }

    /// <summary>
    /// Gets or sets the audio codec.
    /// </summary>
    int AudioCodec { get; set; }

    /// <summary>
    /// Gets or sets the path to the adb executable.
    /// </summary>
    string? AdbPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically connect to devices.
    /// </summary>
    bool AutoConnect { get; set; }

    /// <summary>
    /// Gets or sets the device selection mode for screen mirroring.
    /// </summary>
    ScrcpyDevicePreferenceType ScrcpyDevicePreference { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable virtual display.
    /// </summary>
    bool IsVirtualDisplayEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sync media/audio sessions.
    /// </summary>
    bool MediaSessionSyncEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable TCP/IP mode for ADB.
    /// </summary>
    bool AdbTcpipModeEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically connect via ADB.
    /// </summary>
    bool AdbAutoConnect { get; set; }
} 
