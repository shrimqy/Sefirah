using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Utils.Serialization;

namespace Sefirah.Services.Settings;

internal sealed class DeviceSettingsService : BaseDeviceAwareJsonSettings, IDeviceSettingsService
{
    public DeviceSettingsService(string deviceId, ISettingsSharingContext settingsSharingContext)
        : base(deviceId, settingsSharingContext)
    {
    }

    public bool ClipboardSyncEnabled 
    { 
        get => Get(true);
        set => Set(value);
    }

    public bool ImageToClipboardEnabled 
    { 
        get => Get(false);
        set => Set(value);
    }

    public bool ShowClipboardToast 
    { 
        get => Get(false);
        set => Set(value);
    }

    public bool OpenLinksInBrowser 
    { 
        get => Get(false);
        set => Set(value);
    }   

    public bool NotificationSyncEnabled 
    { 
        get => Get(true);
        set => Set(value);
    }

    public bool ShowNotificationToast 
    { 
        get => Get(true);
        set => Set(value);
    }

    public bool ShowBadge 
    { 
        get => Get(true);
        set => Set(value);
    }

    public NotificationLaunchPreference NotificationLaunchPreference 
    { 
        get => Get(NotificationLaunchPreference.Dynamic);
        set => Set((long)value);
    }

    public string RemoteStoragePath 
    {
        get => Get(Constants.UserEnvironmentPaths.DefaultRemoteDevicePath);
        set => Set(value);
    }

    public string ReceivedFilesPath 
    { 
        get => Get(Constants.UserEnvironmentPaths.DownloadsPath);
        set => Set(value);
    }

    public bool IgnoreWindowsApps 
    { 
        get => Get(true);
        set => Set(value);
    }

    public bool IgnoreNotificationDuringDnd
    {
        get => Get(true);
        set => Set(value);
    }

    public bool ClipboardFilesEnabled 
    { 
        get => Get(false);
        set => Set(value);
    }

    public string? ScrcpyPath
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public bool ScreenOff
    {
        get => Get(true);
        set => Set(value);
    }

    public bool PhysicalKeyboard
    {
        get => Get(false);
        set => Set(value);
    }

    public bool UnlockDeviceBeforeLaunch
    {
        get => Get(false);
        set => Set(value);
    }

    public int UnlockTimeout
    {
        get => Get(0);
        set => Set(value);
    }
    
    public string? UnlockCommands
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public string? VideoBitrate
    {
        get => Get("8M");
        set => Set(value);
    }

    public string? VideoResolution
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public string? VideoBuffer
    {
        get => Get("0");
        set => Set(value);
    }
    
    public string? AudioBitrate
    {
        get => Get("128K");
        set => Set(value);
    }   
    
    public string? AudioBuffer
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public string? CustomArguments
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public bool DisableVideoForwarding
    {
        get => Get(false);
        set => Set(value);
    }

    public int VideoCodec
    {
        get => Get(0);
        set => Set(value);
    }
     
    public string? FrameRate
    {
        get => Get("60");
        set => Set(value);
    }

    public string? Crop
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public string? Display
    {
        get => Get("0");
        set => Set(value);
    }

    public string? VirtualDisplaySize
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public int DisplayOrientation
    {
        get => Get(0);
        set => Set(value);
    }

    public string? RotationAngle
    {
        get => Get("0");
        set => Set(value);
    }

    public AudioOutputModeType AudioOutputMode
    {
        get => Get(AudioOutputModeType.Desktop);
        set => Set(value);
    }

    public bool ForwardMicrophone
    {
        get => Get(false);
        set => Set(value);
    }

    public string? AudioOutputBuffer
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public int AudioCodec
    {
        get => Get(0);
        set => Set(value);
    }

    public string? AdbPath
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public bool AutoConnect
    {
        get => Get(true);
        set => Set(value);
    }

    public ScrcpyDevicePreferenceType ScrcpyDevicePreference
    {
        get => Get(ScrcpyDevicePreferenceType.Auto);
        set => Set(value);
    }

    public bool IsVirtualDisplayEnabled
    {
        get => Get(true);
        set => Set(value);
    }

    public bool MediaSessionSyncEnabled
    {
        get => Get(true);
        set => Set(value);
    }

    public bool AdbTcpipModeEnabled
    {
        get => Get(false);
        set => Set(value);
    }

    public bool AdbAutoConnect
    {
        get => Get(true);
        set => Set(value);
    }
} 
