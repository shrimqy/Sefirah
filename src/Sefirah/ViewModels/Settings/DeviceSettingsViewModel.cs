using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Items;
using Sefirah.Data.Models;
using Sefirah.Extensions;

namespace Sefirah.ViewModels.Settings;

public sealed partial class DeviceSettingsViewModel : BaseViewModel
{
    #region Display Properties
    public string DisplayPhoneNumbers
    {
        get
        {
            if (Device?.PhoneNumbers == null || Device.PhoneNumbers.Count == 0)
                return "No phone numbers";

            return string.Join(", ", Device.PhoneNumbers.Select(p => p.Number));
        }
    }

    public string DisplayIpAddresses
    {
        get
        {
            if (Device?.IpAddresses == null || Device.IpAddresses.Count == 0)
                return "No IP addresses";

            return string.Join(", ", Device.IpAddresses);
        }
    }
    #endregion

    #region Clipboard Settings
    public bool ClipboardSyncEnabled
    {
        get => DeviceSettings?.ClipboardSyncEnabled ?? true;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ClipboardSyncEnabled != value)
            {
                DeviceSettings.ClipboardSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool OpenLinksInBrowser
    {
        get => DeviceSettings?.OpenLinksInBrowser ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.OpenLinksInBrowser != value)
            {
                DeviceSettings.OpenLinksInBrowser = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowClipboardToast
    {
        get => DeviceSettings?.ShowClipboardToast ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ShowClipboardToast != value)
            {
                DeviceSettings.ShowClipboardToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ClipboardFilesEnabled
    {
        get => DeviceSettings?.ClipboardFilesEnabled ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ClipboardFilesEnabled != value)
            {
                DeviceSettings.ClipboardFilesEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ImageToClipboardEnabled
    {
        get => DeviceSettings?.ImageToClipboardEnabled ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ImageToClipboardEnabled != value)
            {
                DeviceSettings.ImageToClipboardEnabled = value;
                OnPropertyChanged();
            }
        }
    }
    #endregion

    #region Notification Settings
    public bool NotificationSyncEnabled
    {
        get => DeviceSettings?.NotificationSyncEnabled ?? true;
        set
        {
            if (DeviceSettings != null && DeviceSettings.NotificationSyncEnabled != value)
            {
                DeviceSettings.NotificationSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowNotificationToast
    {
        get => DeviceSettings?.ShowNotificationToast ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ShowNotificationToast != value)
            {
                DeviceSettings.ShowNotificationToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowBadge
    {
        get => DeviceSettings?.ShowBadge ?? true;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ShowBadge != value)
            {
                DeviceSettings.ShowBadge = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreWindowsApps
    {
        get => DeviceSettings?.IgnoreWindowsApps ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.IgnoreWindowsApps != value)
            {
                DeviceSettings.IgnoreWindowsApps = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreNotificationDuringDnd
    {
        get => DeviceSettings?.IgnoreNotificationDuringDnd ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.IgnoreNotificationDuringDnd != value)
            {
                DeviceSettings.IgnoreNotificationDuringDnd = value;
                OnPropertyChanged();
            }
        }
    }
    #endregion

    #region Screen Mirror settings

    public bool IsGeneralScreenMirrorSettingsExpanded { get; set; } = true;

    #region General Settings

    public ObservableCollection<ScrcpyPreferenceItem> DisplayOrientationOptions => AdbService.DisplayOrientationOptions;
    public ObservableCollection<ScrcpyPreferenceItem> VideoCodecOptions => AdbService.VideoCodecOptions;
    public ObservableCollection<ScrcpyPreferenceItem> AudioCodecOptions => AdbService.AudioCodecOptions;

    public Dictionary<ScrcpyDevicePreferenceType, string> ScrcpyDevicePreferenceOptions { get; } = new()
    {
        { ScrcpyDevicePreferenceType.Auto, "Auto".GetLocalizedResource() },
        { ScrcpyDevicePreferenceType.Usb, "USB" },
        { ScrcpyDevicePreferenceType.Tcpip, "WIFI" },
        { ScrcpyDevicePreferenceType.AskEverytime, "AskEverytime".GetLocalizedResource() }
    };

    private string? selectedScrcpyDevicePreference;
    public string? SelectedScrcpyDevicePreference
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
        get => DeviceSettings?.ScrcpyDevicePreference ?? ScrcpyDevicePreferenceType.Auto;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ScrcpyDevicePreference != value)
            {
                DeviceSettings.ScrcpyDevicePreference = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ScreenOff
    {
        get => DeviceSettings?.ScreenOff ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ScreenOff != value)
            {
                DeviceSettings.ScreenOff = value;
                OnPropertyChanged();
            }
        }
    }

    public bool PhysicalKeyboard
    {
        get => DeviceSettings?.PhysicalKeyboard ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.PhysicalKeyboard != value)
            {
                DeviceSettings.PhysicalKeyboard = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UnlockDeviceBeforeLaunch
    {
        get => DeviceSettings?.UnlockDeviceBeforeLaunch ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.UnlockDeviceBeforeLaunch != value)
            {
                DeviceSettings.UnlockDeviceBeforeLaunch = value;
                OnPropertyChanged();
            }
        }
    }

    public int UnlockTimeout
    {
        get => DeviceSettings?.UnlockTimeout ?? 0;
        set
        {
            if (DeviceSettings != null && DeviceSettings.UnlockTimeout != value)
            {
                DeviceSettings.UnlockTimeout = value;
                OnPropertyChanged();
            }
        }
    }

    public string? UnlockCommands
    {
        get => DeviceSettings?.UnlockCommands;
        set
        {
            if (DeviceSettings != null && DeviceSettings.UnlockCommands != value)
            {
                DeviceSettings.UnlockCommands = value;
                OnPropertyChanged();
            }
        }
    }


    public string? CustomArguments
    {
        get => DeviceSettings?.CustomArguments;
        set
        {
            if (DeviceSettings != null && DeviceSettings.CustomArguments != value)
            {
                DeviceSettings.CustomArguments = value;
                OnPropertyChanged();
            }
        }
    }
    #endregion

    #region Video Settings

    public bool DisableVideoForwarding
    {
        get => DeviceSettings?.DisableVideoForwarding ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.DisableVideoForwarding != value)
            {
                DeviceSettings.DisableVideoForwarding = value;
                OnPropertyChanged();
            }
        }
    }

    public int VideoCodec
    {
        get => DeviceSettings?.VideoCodec ?? 0;
        set
        {
            if (DeviceSettings != null && DeviceSettings.VideoCodec != value)
            {
                DeviceSettings.VideoCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VideoBitrate
    {
        get => DeviceSettings?.VideoBitrate;
        set
        {
            if (DeviceSettings != null && DeviceSettings.VideoBitrate != value)
            {
                DeviceSettings.VideoBitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public string? FrameRate
    {
        get => DeviceSettings?.FrameRate;
        set
        {
            if (DeviceSettings != null && DeviceSettings.FrameRate != value)
            {
                DeviceSettings.FrameRate = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Crop
    {
        get => DeviceSettings?.Crop;
        set
        {
            if (DeviceSettings != null && DeviceSettings.Crop != value)
            {
                DeviceSettings.Crop = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Display
    {
        get => DeviceSettings?.Display;
        set
        {
            if (DeviceSettings != null && DeviceSettings.Display != value)
            {
                DeviceSettings.Display = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsVirtualDisplayEnabled
    {
        get => DeviceSettings?.IsVirtualDisplayEnabled ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.IsVirtualDisplayEnabled != value)
            {
                DeviceSettings.IsVirtualDisplayEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VirtualDisplaySize
    {
        get => DeviceSettings?.VirtualDisplaySize;
        set
        {
            if (DeviceSettings != null && DeviceSettings.VirtualDisplaySize != value)
            {
                DeviceSettings.VirtualDisplaySize = value;
                OnPropertyChanged();
            }
        }
    }

    public int DisplayOrientation
    {
        get => DeviceSettings?.DisplayOrientation ?? 0;
        set
        {
            if (DeviceSettings != null && DeviceSettings.DisplayOrientation != value)
            {
                DeviceSettings.DisplayOrientation = value;
                OnPropertyChanged();
            }
        }
    }

    public string? RotationAngle
    {
        get => DeviceSettings?.RotationAngle;
        set
        {
            if (DeviceSettings != null && DeviceSettings.RotationAngle != value)
            {
                DeviceSettings.RotationAngle = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VideoBuffer
    {
        get => DeviceSettings?.VideoBuffer;
        set
        {
            if (DeviceSettings != null && DeviceSettings.VideoBuffer != value)
            {
                DeviceSettings.VideoBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Audio Settings

    public AudioOutputModeType AudioOutputMode
    {
        get => DeviceSettings?.AudioOutputMode ?? AudioOutputModeType.Desktop;
        set
        {
            if (DeviceSettings != null && DeviceSettings.AudioOutputMode != value)
            {
                DeviceSettings.AudioOutputMode = value;
                OnPropertyChanged();
            }
        }
    }

    private string? selectedAudioOutputMode;
    public string? SelectedAudioOutputMode
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
        get => DeviceSettings?.AudioBitrate;
        set
        {
            if (DeviceSettings != null && DeviceSettings.AudioBitrate != value)
            {
                DeviceSettings.AudioBitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ForwardMicrophone
    {
        get => DeviceSettings?.ForwardMicrophone ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.ForwardMicrophone != value)
            {
                DeviceSettings.ForwardMicrophone = value;
                OnPropertyChanged();
            }
        }
    }

    public int AudioCodec
    {
        get => DeviceSettings?.AudioCodec ?? 0;
        set
        {
            if (DeviceSettings != null && DeviceSettings.AudioCodec != value)
            {
                DeviceSettings.AudioCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string? AudioOutputBuffer
    {
        get => DeviceSettings?.AudioOutputBuffer;
        set
        {
            if (DeviceSettings != null && DeviceSettings.AudioOutputBuffer != value)
            {
                DeviceSettings.AudioOutputBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    public string? AudioBuffer
    {
        get => DeviceSettings?.AudioBuffer;
        set
        {
            if (DeviceSettings != null && DeviceSettings.AudioBuffer != value)
            {
                DeviceSettings.AudioBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #endregion

    #region Media Session Settings

    public bool MediaSessionSyncEnabled
    {
        get => DeviceSettings?.MediaSessionSyncEnabled ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.MediaSessionSyncEnabled != value)
            {
                DeviceSettings.MediaSessionSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region ADB Settings

    public bool AdbTcpipModeEnabled
    {
        get => DeviceSettings?.AdbTcpipModeEnabled ?? false;
        set
        {
            if (DeviceSettings != null && DeviceSettings.AdbTcpipModeEnabled != value)
            {
                DeviceSettings.AdbTcpipModeEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AdbAutoConnect
    {
        get => DeviceSettings?.AdbAutoConnect ?? true;
        set
        {
            if (DeviceSettings != null && DeviceSettings.AdbAutoConnect != value)
            {
                DeviceSettings.AdbAutoConnect = value;
                OnPropertyChanged();
            }
        }
    }


    private async Task ShowErrorDialog(string title, string content)
    {
        await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                XamlRoot = App.MainWindow.Content?.XamlRoot,
                Title = title,
                Content = content,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        });
    }

    #endregion


    private readonly IAdbService AdbService = Ioc.Default.GetRequiredService<IAdbService>();
    private IDeviceSettingsService? DeviceSettings;

    private readonly RemoteAppRepository RemoteAppsRepository = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    public ObservableCollection<ApplicationInfo> RemoteApps { get; set; } = [];

    private PairedDevice? device;
    public PairedDevice? Device 
    { 
        get => device;
        set 
        {
            SetProperty(ref device, value);
            OnPropertyChanged(nameof(DisplayPhoneNumbers));
            OnPropertyChanged(nameof(DisplayIpAddresses));
        }
    }

    public void SetDevice(PairedDevice device)
    {
        Device = device;
        
        device.PropertyChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(Device));
        };

        DeviceSettings = device.DeviceSettings;
        OnPropertyChanged(nameof(DeviceSettings));

        selectedAudioOutputMode = AudioOutputModeOptions[AudioOutputMode];
        selectedScrcpyDevicePreference = ScrcpyDevicePreferenceOptions[ScrcpyDevicePreference];
        
        OnPropertyChanged(nameof(SelectedAudioOutputMode));
        OnPropertyChanged(nameof(SelectedScrcpyDevicePreference));

        LoadApps(device.Id);
    }

    public void LoadApps(string id)
    {
        RemoteApps = RemoteAppsRepository.GetApplicationsForDevice(id);
    }

    public void ChangeNotificationFilter(string notificationFilter, string appPackage)
    {
        var filterKey = ApplicationInfo.NotificationFilterTypes.First(f => f.Value == notificationFilter).Key;
        RemoteAppsRepository.UpdateAppNotificationFilter(device!.Id, appPackage, filterKey);
        var app = RemoteApps.First(p => p.PackageName == appPackage);
        app.DeviceInfo.Filter = filterKey;
        app.SelectedNotificationFilter = notificationFilter;
    }
}
