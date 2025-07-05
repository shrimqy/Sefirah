using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Items;
using Sefirah.Data.Models;
using Sefirah.Extensions;
using Sefirah.Services;
using Sefirah.Services.Settings;

namespace Sefirah.ViewModels.Settings;

public sealed partial class DeviceSettingsViewModel : BaseViewModel
{
    #region Display Properties
    public string DisplayPhoneNumbers
    {
        get
        {
            if (Device?.PhoneNumbers == null || !Device.PhoneNumbers.Any())
                return "No phone numbers";

            return string.Join(", ", Device.PhoneNumbers.Select(p => p.Number));
        }
    }

    public string DisplayIpAddresses
    {
        get
        {
            if (Device?.IpAddresses == null || !Device.IpAddresses.Any())
                return "No IP addresses";

            return string.Join(", ", Device.IpAddresses);
        }
    }
    #endregion

    #region Clipboard Settings
    public bool ClipboardSyncEnabled
    {
        get => _deviceSettings?.ClipboardSyncEnabled ?? true;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ClipboardSyncEnabled != value)
            {
                _deviceSettings.ClipboardSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool OpenLinksInBrowser
    {
        get => _deviceSettings?.OpenLinksInBrowser ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.OpenLinksInBrowser != value)
            {
                _deviceSettings.OpenLinksInBrowser = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowClipboardToast
    {
        get => _deviceSettings?.ShowClipboardToast ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ShowClipboardToast != value)
            {
                _deviceSettings.ShowClipboardToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ClipboardFilesEnabled
    {
        get => _deviceSettings?.ClipboardFilesEnabled ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ClipboardFilesEnabled != value)
            {
                _deviceSettings.ClipboardFilesEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ImageToClipboardEnabled
    {
        get => _deviceSettings?.ImageToClipboardEnabled ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ImageToClipboardEnabled != value)
            {
                _deviceSettings.ImageToClipboardEnabled = value;
                OnPropertyChanged();
            }
        }
    }
    #endregion

    #region Notification Settings
    public bool NotificationSyncEnabled
    {
        get => _deviceSettings?.NotificationSyncEnabled ?? true;
        set
        {
            if (_deviceSettings != null && _deviceSettings.NotificationSyncEnabled != value)
            {
                _deviceSettings.NotificationSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowNotificationToast
    {
        get => _deviceSettings?.ShowNotificationToast ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ShowNotificationToast != value)
            {
                _deviceSettings.ShowNotificationToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowBadge
    {
        get => _deviceSettings?.ShowBadge ?? true;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ShowBadge != value)
            {
                _deviceSettings.ShowBadge = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreWindowsApps
    {
        get => _deviceSettings?.IgnoreWindowsApps ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.IgnoreWindowsApps != value)
            {
                _deviceSettings.IgnoreWindowsApps = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreNotificationDuringDnd
    {
        get => _deviceSettings?.IgnoreNotificationDuringDnd ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.IgnoreNotificationDuringDnd != value)
            {
                _deviceSettings.IgnoreNotificationDuringDnd = value;
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
        get => _deviceSettings?.ScrcpyDevicePreference ?? ScrcpyDevicePreferenceType.Auto;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ScrcpyDevicePreference != value)
            {
                _deviceSettings.ScrcpyDevicePreference = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ScreenOff
    {
        get => _deviceSettings?.ScreenOff ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ScreenOff != value)
            {
                _deviceSettings.ScreenOff = value;
                OnPropertyChanged();
            }
        }
    }

    public bool PhysicalKeyboard
    {
        get => _deviceSettings?.PhysicalKeyboard ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.PhysicalKeyboard != value)
            {
                _deviceSettings.PhysicalKeyboard = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UnlockDeviceBeforeLaunch
    {
        get => _deviceSettings?.UnlockDeviceBeforeLaunch ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.UnlockDeviceBeforeLaunch != value)
            {
                _deviceSettings.UnlockDeviceBeforeLaunch = value;
                OnPropertyChanged();
            }
        }
    }

    public string? UnlockCommands
    {
        get => _deviceSettings?.UnlockCommands;
        set
        {
            if (_deviceSettings != null && _deviceSettings.UnlockCommands != value)
            {
                _deviceSettings.UnlockCommands = value;
                OnPropertyChanged();
            }
        }
    }


    public string? CustomArguments
    {
        get => _deviceSettings?.CustomArguments;
        set
        {
            if (_deviceSettings != null && _deviceSettings.CustomArguments != value)
            {
                _deviceSettings.CustomArguments = value;
                OnPropertyChanged();
            }
        }
    }
    #endregion

    #region Video Settings

    public bool DisableVideoForwarding
    {
        get => _deviceSettings?.DisableVideoForwarding ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.DisableVideoForwarding != value)
            {
                _deviceSettings.DisableVideoForwarding = value;
                OnPropertyChanged();
            }
        }
    }

    public int VideoCodec
    {
        get => _deviceSettings?.VideoCodec ?? 0;
        set
        {
            if (_deviceSettings != null && _deviceSettings.VideoCodec != value)
            {
                _deviceSettings.VideoCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VideoBitrate
    {
        get => _deviceSettings?.VideoBitrate;
        set
        {
            if (_deviceSettings != null && _deviceSettings.VideoBitrate != value)
            {
                _deviceSettings.VideoBitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public string? FrameRate
    {
        get => _deviceSettings?.FrameRate;
        set
        {
            if (_deviceSettings != null && _deviceSettings.FrameRate != value)
            {
                _deviceSettings.FrameRate = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Crop
    {
        get => _deviceSettings?.Crop;
        set
        {
            if (_deviceSettings != null && _deviceSettings.Crop != value)
            {
                _deviceSettings.Crop = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Display
    {
        get => _deviceSettings?.Display;
        set
        {
            if (_deviceSettings != null && _deviceSettings.Display != value)
            {
                _deviceSettings.Display = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsVirtualDisplayEnabled
    {
        get => _deviceSettings?.IsVirtualDisplayEnabled ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.IsVirtualDisplayEnabled != value)
            {
                _deviceSettings.IsVirtualDisplayEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VirtualDisplaySize
    {
        get => _deviceSettings?.VirtualDisplaySize;
        set
        {
            if (_deviceSettings != null && _deviceSettings.VirtualDisplaySize != value)
            {
                _deviceSettings.VirtualDisplaySize = value;
                OnPropertyChanged();
            }
        }
    }

    public int DisplayOrientation
    {
        get => _deviceSettings?.DisplayOrientation ?? 0;
        set
        {
            if (_deviceSettings != null && _deviceSettings.DisplayOrientation != value)
            {
                _deviceSettings.DisplayOrientation = value;
                OnPropertyChanged();
            }
        }
    }

    public string? RotationAngle
    {
        get => _deviceSettings?.RotationAngle;
        set
        {
            if (_deviceSettings != null && _deviceSettings.RotationAngle != value)
            {
                _deviceSettings.RotationAngle = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VideoBuffer
    {
        get => _deviceSettings?.VideoBuffer;
        set
        {
            if (_deviceSettings != null && _deviceSettings.VideoBuffer != value)
            {
                _deviceSettings.VideoBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Audio Settings

    public AudioOutputModeType AudioOutputMode
    {
        get => _deviceSettings?.AudioOutputMode ?? AudioOutputModeType.Desktop;
        set
        {
            if (_deviceSettings != null && _deviceSettings.AudioOutputMode != value)
            {
                _deviceSettings.AudioOutputMode = value;
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
        get => _deviceSettings?.AudioBitrate;
        set
        {
            if (_deviceSettings != null && _deviceSettings.AudioBitrate != value)
            {
                _deviceSettings.AudioBitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ForwardMicrophone
    {
        get => _deviceSettings?.ForwardMicrophone ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.ForwardMicrophone != value)
            {
                _deviceSettings.ForwardMicrophone = value;
                OnPropertyChanged();
            }
        }
    }

    public int AudioCodec
    {
        get => _deviceSettings?.AudioCodec ?? 0;
        set
        {
            if (_deviceSettings != null && _deviceSettings.AudioCodec != value)
            {
                _deviceSettings.AudioCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string? AudioOutputBuffer
    {
        get => _deviceSettings?.AudioOutputBuffer;
        set
        {
            if (_deviceSettings != null && _deviceSettings.AudioOutputBuffer != value)
            {
                _deviceSettings.AudioOutputBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    public string? AudioBuffer
    {
        get => _deviceSettings?.AudioBuffer;
        set
        {
            if (_deviceSettings != null && _deviceSettings.AudioBuffer != value)
            {
                _deviceSettings.AudioBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #endregion

    #region Media Session Settings

    public bool MediaSessionSyncEnabled
    {
        get => _deviceSettings?.MediaSessionSyncEnabled ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.MediaSessionSyncEnabled != value)
            {
                _deviceSettings.MediaSessionSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region ADB Settings

    public bool AdbTcpipModeEnabled
    {
        get => _deviceSettings?.AdbTcpipModeEnabled ?? false;
        set
        {
            if (_deviceSettings != null && _deviceSettings.AdbTcpipModeEnabled != value)
            {
                _deviceSettings.AdbTcpipModeEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AdbAutoConnect
    {
        get => _deviceSettings?.AdbAutoConnect ?? true;
        set
        {
            if (_deviceSettings != null && _deviceSettings.AdbAutoConnect != value)
            {
                _deviceSettings.AdbAutoConnect = value;
                OnPropertyChanged();
            }
        }
    }

    private async void OnAdbDevicesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        try
        {
            // Update ADB connection status for the current device
            if (App.MainWindow?.DispatcherQueue != null && Device != null)
            {
                await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                {
                    try
                    {
                        Device.RefreshAdbStatus();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error refreshing ADB status: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw to avoid crashing the app
            System.Diagnostics.Debug.WriteLine($"Error updating ADB display properties: {ex.Message}");
        }
    }

    private async Task ShowErrorDialog(string title, string content)
    {
        await App.MainWindow?.DispatcherQueue.EnqueueAsync(async () =>
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

    private readonly IUserSettingsService _userSettingsService;
    private IDeviceSettingsService? _deviceSettings;

    private readonly IAdbService AdbService = Ioc.Default.GetRequiredService<IAdbService>();

    private PairedDevice? device;
    public PairedDevice? Device 
    { 
        get => device;
        set 
        {
            SetProperty(ref device, value);
            // Update computed properties when device changes
            OnPropertyChanged(nameof(DisplayPhoneNumbers));
            OnPropertyChanged(nameof(DisplayIpAddresses));
        }
    }
    private readonly RemoteAppRepository RemoteAppsRepository = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    public ObservableCollection<ApplicationInfoEntity> RemoteApps { get; set; } = [];

    public DeviceSettingsViewModel()
    {
        _userSettingsService = Ioc.Default.GetService<IUserSettingsService>()!;
    }

    public IDeviceSettingsService? DeviceSettings => _deviceSettings;

    public void SetDevice(PairedDevice device)
    {
        Device = device;
        
        device.PropertyChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(Device));
        };

        _deviceSettings = device.DeviceSettings;
        OnPropertyChanged(nameof(DeviceSettings));

        selectedAudioOutputMode = AudioOutputModeOptions[AudioOutputMode];
        selectedScrcpyDevicePreference = ScrcpyDevicePreferenceOptions[ScrcpyDevicePreference];
        
        OnPropertyChanged(nameof(SelectedAudioOutputMode));
        OnPropertyChanged(nameof(SelectedScrcpyDevicePreference));

        // Subscribe to ADB device changes to update connection status
        AdbService.AdbDevices.CollectionChanged += OnAdbDevicesChanged;

        // Initial ADB status refresh
        Device.RefreshAdbStatus();

        RemoteApps = RemoteAppsRepository.GetApplicationsFromDevice(device.Id).ToObservableCollection();
        foreach (var app in RemoteApps)
        {
            app.UpdateNotificationFilter(device.Id);
        }
    }



    public void ChangeNotificationFilter(string notificationFilter, string appPackage)
    {
        var filterKey = ApplicationInfoEntity.NotificationFilterTypes.First(f => f.Value == notificationFilter).Key;
        RemoteAppsRepository.UpdateAppNotificationFilter(device!.Id, appPackage, filterKey);
        var existingItem = RemoteApps.First(p => p.AppPackage == appPackage);
        existingItem.CurrentNotificationFilter = notificationFilter;
    }
}
