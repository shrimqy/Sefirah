using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Utils.Serialization;

namespace Sefirah.App.Services.Settings;
internal sealed class FeaturesSettingsService : BaseObservableJsonSettings, IFeatureSettingsService
{
    public FeaturesSettingsService(ISettingsSharingContext settingsSharingContext)
    {
        RegisterSettingsContext(settingsSharingContext);
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
        get => (NotificationLaunchPreference)Get((long)NotificationLaunchPreference.Dynamic);
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
        get => Get(true);
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
        get => Get("20");
        set => Set(value);
    }
}
