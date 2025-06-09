using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Items;
using Sefirah.Extensions;
using Sefirah.Services;

namespace Sefirah.ViewModels.Settings;
public sealed partial class FeaturesViewModel : BaseViewModel
{
    private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
    private readonly RemoteAppRepository RemoteAppsRepository = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    private readonly IAdbService AdbService = Ioc.Default.GetRequiredService<IAdbService>();

    #region Properties
    public bool IsClipboardExpanded { get; set; }
    public bool IsNotificationExpanded { get; set; }
    public bool IsNotificationGeneralSettingsExpanded { get; set; } = true;
    public bool IsAppNotificationExpanded { get; set; }
    public bool IsScreenMirrorExpanded { get; set; }
    public bool IsGeneralSettingsExpanded { get; set; } = true;
    public bool IsVideoSettingsExpanded { get; set; }
    public bool IsAudioSettingsExpanded { get; set; }
    public bool IsAdbSettingsExpanded { get; set; }
    #endregion

    #region Clipboard Settings
    public bool ClipboardSyncEnabled
    {
        get => UserSettingsService.FeatureSettingsService.ClipboardSyncEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ClipboardSyncEnabled)
            {
                UserSettingsService.FeatureSettingsService.ClipboardSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }
    public bool ShowClipboardToast
    {
        get => UserSettingsService.FeatureSettingsService.ShowClipboardToast;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ShowClipboardToast)
            {
                UserSettingsService.FeatureSettingsService.ShowClipboardToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool OpenLinksInBrowser
    {
        get => UserSettingsService.FeatureSettingsService.OpenLinksInBrowser;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.OpenLinksInBrowser)
            {
                UserSettingsService.FeatureSettingsService.OpenLinksInBrowser = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ClipboardFilesEnabled
    {
        get => UserSettingsService.FeatureSettingsService.ClipboardFilesEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ClipboardFilesEnabled)
            {
                UserSettingsService.FeatureSettingsService.ClipboardFilesEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ImageToClipboardEnabled
    {
        get => UserSettingsService.FeatureSettingsService.ImageToClipboardEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ImageToClipboardEnabled)
            {
                UserSettingsService.FeatureSettingsService.ImageToClipboardEnabled = value;
                OnPropertyChanged();
            }
        }
    }
    #endregion

    #region Notification Settings

    public bool NotificationSyncEnabled
    {
        get => UserSettingsService.FeatureSettingsService.NotificationSyncEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.NotificationSyncEnabled)
            {
                UserSettingsService.FeatureSettingsService.NotificationSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowNotificationToast
    {
        get => UserSettingsService.FeatureSettingsService.ShowNotificationToast;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ShowNotificationToast)
            {
                UserSettingsService.FeatureSettingsService.ShowNotificationToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreWindowsApps
    {
        get => UserSettingsService.FeatureSettingsService.IgnoreWindowsApps;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.IgnoreWindowsApps)
            {
                UserSettingsService.FeatureSettingsService.IgnoreWindowsApps = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreNotificationDuringDnd
    {
        get => UserSettingsService.FeatureSettingsService.IgnoreNotificationDuringDnd;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.IgnoreNotificationDuringDnd)
            {
                UserSettingsService.FeatureSettingsService.IgnoreNotificationDuringDnd = value;
                OnPropertyChanged();
            }
        }
    }

    // TODO
    public NotificationLaunchPreference NotificationLaunchPreference
    {
        get => UserSettingsService.FeatureSettingsService.NotificationLaunchPreference;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.NotificationLaunchPreference)
            {
                UserSettingsService.FeatureSettingsService.NotificationLaunchPreference = value;
                OnPropertyChanged();
            }
        }
    }

    private async void LoadNotificationPreferencesAsync()
    {
        //await RemoteAppsRepository.LoadApplicationsAsync();
    }

    public async void ChangeNotificationFilter(ApplicationInfoEntity preferences)
    {
        //await RemoteAppsRepository.UpdateFilterAsync(preferences.AppPackage, preferences.NotificationFilter);
        //var existingItem = NotificationPreferences.FirstOrDefault(p => p.AppPackage == preferences.AppPackage);

        //if (existingItem != null)
        //{
        //    dispatcherQueue.TryEnqueue(() =>
        //    {
        //        existingItem.NotificationFilter = preferences.NotificationFilter;
        //    });
        //}
    }
    #endregion

    #region Screen Mirror settings

    #region General Settings
    public string? ScrcpyPath
    {
        get => UserSettingsService.FeatureSettingsService.ScrcpyPath;
        set
        {
            UserSettingsService.FeatureSettingsService.ScrcpyPath = value;
            OnPropertyChanged();
        }
    }

    public Dictionary<ScrcpyDevicePreferenceType, string> ScrcpyDevicePreferenceOptions { get; } = new()
    {
        { ScrcpyDevicePreferenceType.Auto, "Auto".GetLocalizedResource() },
        { ScrcpyDevicePreferenceType.Usb, "USB".GetLocalizedResource() },
        { ScrcpyDevicePreferenceType.Tcpip, "WIFI".GetLocalizedResource() },
        { ScrcpyDevicePreferenceType.AskEverytime, "AskEverytime".GetLocalizedResource() }
    };

    private string selectedScrcpyDevicePreference;
    public string SelectedScrcpyDevicePreference
    {
        get => selectedScrcpyDevicePreference;
        set
        {
            if (SetProperty(ref selectedScrcpyDevicePreference, value))
            {
                ScrcpyDevicePreference = ScrcpyDevicePreferenceOptions.First(e => e.Value == value).Key;
            }
        }
    }

    public ScrcpyDevicePreferenceType ScrcpyDevicePreference
    {
        get => UserSettingsService.FeatureSettingsService.ScrcpyDevicePreference;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ScrcpyDevicePreference)
            {
                UserSettingsService.FeatureSettingsService.ScrcpyDevicePreference = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ScreenOff
    {
        get => UserSettingsService.FeatureSettingsService.ScreenOff;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ScreenOff)
            {
                UserSettingsService.FeatureSettingsService.ScreenOff = value;
                OnPropertyChanged();
            }
        }
    }

    public bool PhysicalKeyboard
    {
        get => UserSettingsService.FeatureSettingsService.PhysicalKeyboard;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.PhysicalKeyboard)
            {
                UserSettingsService.FeatureSettingsService.PhysicalKeyboard = value;
                OnPropertyChanged();
            }
        }
    }

    public string? CustomArguments
    {
        get => UserSettingsService.FeatureSettingsService.CustomArguments;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.CustomArguments)
            {
                UserSettingsService.FeatureSettingsService.CustomArguments = value;
                OnPropertyChanged();
            }
        }
    }
    #endregion

    #region Video Settings

    public bool DisableVideoForwarding
    {
        get => UserSettingsService.FeatureSettingsService.DisableVideoForwarding;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.DisableVideoForwarding)
            {
                UserSettingsService.FeatureSettingsService.DisableVideoForwarding = value;
                OnPropertyChanged();
            }
        }
    }

    public int VideoCodec
    {
        get => UserSettingsService.FeatureSettingsService.VideoCodec;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.VideoCodec)
            {
                UserSettingsService.FeatureSettingsService.VideoCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VideoBitrate
    {
        get => UserSettingsService.FeatureSettingsService.VideoBitrate;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.VideoBitrate)
            {
                UserSettingsService.FeatureSettingsService.VideoBitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public string? FrameRate
    {
        get => UserSettingsService.FeatureSettingsService.FrameRate;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.FrameRate)
            {
                UserSettingsService.FeatureSettingsService.FrameRate = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Crop
    {
        get => UserSettingsService.FeatureSettingsService.Crop;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.Crop)
            {
                UserSettingsService.FeatureSettingsService.Crop = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Display
    {
        get => UserSettingsService.FeatureSettingsService.Display;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.Display)
            {
                UserSettingsService.FeatureSettingsService.Display = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsVirtualDisplayEnabled
    {
        get => UserSettingsService.FeatureSettingsService.IsVirtualDisplayEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.IsVirtualDisplayEnabled)
            {
                UserSettingsService.FeatureSettingsService.IsVirtualDisplayEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VirtualDisplaySize
    {
        get => UserSettingsService.FeatureSettingsService.VirtualDisplaySize;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.VirtualDisplaySize)
            {
                UserSettingsService.FeatureSettingsService.VirtualDisplaySize = value;
                OnPropertyChanged();
            }
        }
    }

    public int DisplayOrientation
    {
        get => UserSettingsService.FeatureSettingsService.DisplayOrientation;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.DisplayOrientation)
            {
                UserSettingsService.FeatureSettingsService.DisplayOrientation = value;
                OnPropertyChanged();
            }
        }
    }

    public string? RotationAngle
    {
        get => UserSettingsService.FeatureSettingsService.RotationAngle;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.RotationAngle)
            {
                UserSettingsService.FeatureSettingsService.RotationAngle = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VideoBuffer
    {
        get => UserSettingsService.FeatureSettingsService.VideoBuffer;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.VideoBuffer)
            {
                UserSettingsService.FeatureSettingsService.VideoBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Audio Settings

    public AudioOutputModeType AudioOutputMode
    {
        get => UserSettingsService.FeatureSettingsService.AudioOutputMode;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.AudioOutputMode)
            {
                UserSettingsService.FeatureSettingsService.AudioOutputMode = value;
                OnPropertyChanged();
            }
        }
    }

    private string selectedAudioOutputMode;
    public string SelectedAudioOutputMode
    {
        get => selectedAudioOutputMode;
        set
        {
            if (SetProperty(ref selectedAudioOutputMode, value))
            {
                AudioOutputMode = AudioOutputModeOptions.First(e => e.Value == value).Key;
            }
        }
    }

    public Dictionary<AudioOutputModeType, string> AudioOutputModeOptions { get; } = new()
    {
        { AudioOutputModeType.Desktop, "DesktopDevice".GetLocalizedResource() },
        { AudioOutputModeType.Remote, "RemoteDevice".GetLocalizedResource() },
        { AudioOutputModeType.Both, "Both".GetLocalizedResource() }
    };

    public string? AudioBitrate
    {
        get => UserSettingsService.FeatureSettingsService.AudioBitrate;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.AudioBitrate)
            {
                UserSettingsService.FeatureSettingsService.AudioBitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ForwardMicrophone
    {
        get => UserSettingsService.FeatureSettingsService.ForwardMicrophone;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ForwardMicrophone)
            {
                UserSettingsService.FeatureSettingsService.ForwardMicrophone = value;
                OnPropertyChanged();
            }
        }
    }

    public int AudioCodec
    {
        get => UserSettingsService.FeatureSettingsService.AudioCodec;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.AudioCodec)
            {
                UserSettingsService.FeatureSettingsService.AudioCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string? AudioOutputBuffer
    {
        get => UserSettingsService.FeatureSettingsService.AudioOutputBuffer;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.AudioOutputBuffer)
            {
                UserSettingsService.FeatureSettingsService.AudioOutputBuffer = value;
                OnPropertyChanged();
            }
        }
    }


    public string? AudioBuffer
    {
        get => UserSettingsService.FeatureSettingsService.AudioBuffer;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.AudioBuffer)
            {
                UserSettingsService.FeatureSettingsService.AudioBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #endregion

    public string? AdbPath
    {
        get => UserSettingsService.FeatureSettingsService.AdbPath;
        set
        {
            UserSettingsService.FeatureSettingsService.AdbPath = value;
            //AdbService.StartAsync();
            OnPropertyChanged();
        }
    }

    public bool AutoConnect
    {
        get => UserSettingsService.FeatureSettingsService.AutoConnect;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.AutoConnect)
            {
                UserSettingsService.FeatureSettingsService.AutoConnect = value;
                OnPropertyChanged();
            }
        }
    }
    public string ReceivedFilesPath
    {
        get => UserSettingsService.FeatureSettingsService.ReceivedFilesPath;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ReceivedFilesPath)
            {
                UserSettingsService.FeatureSettingsService.ReceivedFilesPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string RemoteStoragePath
    {
        get => UserSettingsService.FeatureSettingsService.RemoteStoragePath;
        set
        {
            // TODO : Delete the previous remote storage folder or move all the placeholders to the new location
            if (value != UserSettingsService.FeatureSettingsService.RemoteStoragePath)
            {
                UserSettingsService.FeatureSettingsService.RemoteStoragePath = value;
                var sftpService = Ioc.Default.GetRequiredService<ISftpService>();
                //sftpService.RemoveAllSyncRoots();
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<ScrcpyPreferenceItem> DisplayOrientationOptions => AdbService.DisplayOrientationOptions;
    public ObservableCollection<ScrcpyPreferenceItem> VideoCodecOptions => AdbService.VideoCodecOptions;
    public ObservableCollection<ScrcpyPreferenceItem> AudioCodecOptions => AdbService.AudioCodecOptions;

    public FeaturesViewModel()
    {
        LoadNotificationPreferencesAsync();
        selectedAudioOutputMode = AudioOutputModeOptions[AudioOutputMode];
        selectedScrcpyDevicePreference = ScrcpyDevicePreferenceOptions[ScrcpyDevicePreference];
    }
}
