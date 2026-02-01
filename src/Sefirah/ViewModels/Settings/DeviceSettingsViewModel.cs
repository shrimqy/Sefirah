using System.Collections.Specialized;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Items;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;

namespace Sefirah.ViewModels.Settings;

public sealed partial class DeviceSettingsViewModel : BaseViewModel
{
    #region Display Properties
    public string DisplayPhoneNumbers => string.Join(", ", Device.PhoneNumbers.Select(p => p.Number));
    public string DisplayAddresses => string.Join(", ", Device.GetEnabledAddresses());
    #endregion

    #region Clipboard Settings
    public bool ClipboardReceive
    {
        get => DeviceSettings.ClipboardReceive;
        set
        {
            if (DeviceSettings.ClipboardReceive != value)
            {
                DeviceSettings.ClipboardReceive = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ClipboardSend
    {
        get => DeviceSettings.ClipboardSend;
        set
        {
            if (DeviceSettings.ClipboardSend != value)
            {
                DeviceSettings.ClipboardSend = value;
                OnPropertyChanged();
            }
        }
    }

    public bool OpenLinksInBrowser
    {
        get => DeviceSettings.OpenLinksInBrowser;
        set
        {
            if (DeviceSettings.OpenLinksInBrowser != value)
            {
                DeviceSettings.OpenLinksInBrowser = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowClipboardToast
    {
        get => DeviceSettings.ShowClipboardToast;
        set
        {
            if (DeviceSettings.ShowClipboardToast != value)
            {
                DeviceSettings.ShowClipboardToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ClipboardFiles
    {
        get => DeviceSettings.ClipboardFiles;
        set
        {
            if (DeviceSettings.ClipboardFiles != value)
            {
                DeviceSettings.ClipboardFiles = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ClipboardIncludeImages
    {
        get => DeviceSettings.ClipboardIncludeImages;
        set
        {
            if (DeviceSettings.ClipboardIncludeImages != value)
            {
                DeviceSettings.ClipboardIncludeImages = value;
                OnPropertyChanged();
            }
        }
    }
    #endregion

    #region Notification Settings
    public bool NotificationSync
    {
        get => DeviceSettings.NotificationSync;
        set
        {
            if (DeviceSettings.NotificationSync != value)
            {
                DeviceSettings.NotificationSync = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowNotificationToast
    {
        get => DeviceSettings.ShowNotificationToast;
        set
        {
            if (DeviceSettings.ShowNotificationToast != value)
            {
                DeviceSettings.ShowNotificationToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowBadge
    {
        get => DeviceSettings.ShowBadge;
        set
        {
            if (DeviceSettings.ShowBadge != value)
            {
                DeviceSettings.ShowBadge = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreWindowsApps
    {
        get => DeviceSettings.IgnoreWindowsApps;
        set
        {
            if (DeviceSettings.IgnoreWindowsApps != value)
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
            if (DeviceSettings.IgnoreNotificationDuringDnd != value)
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
    public ObservableCollection<ScrcpyPreferenceItem> VideoCodecOptions => AdbService.GetVideoCodecOptions(Device?.Model ?? "Unknown");
    public ObservableCollection<ScrcpyPreferenceItem> AudioCodecOptions => AdbService.GetAudioCodecOptions(Device?.Model ?? "Unknown");

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
        get => DeviceSettings.ScrcpyDevicePreference;
        set
        {
            if (DeviceSettings.ScrcpyDevicePreference != value)
            {
                DeviceSettings.ScrcpyDevicePreference = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ScreenOff
    {
        get => DeviceSettings.ScreenOff;
        set
        {
            if (DeviceSettings.ScreenOff != value)
            {
                DeviceSettings.ScreenOff = value;
                OnPropertyChanged();
            }
        }
    }

    public bool PhysicalKeyboard
    {
        get => DeviceSettings.PhysicalKeyboard;
        set
        {
            if (DeviceSettings.PhysicalKeyboard != value)
            {
                DeviceSettings.PhysicalKeyboard = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UnlockDeviceBeforeLaunch
    {
        get => DeviceSettings.UnlockDeviceBeforeLaunch;
        set
        {
            if (DeviceSettings.UnlockDeviceBeforeLaunch != value)
            {
                DeviceSettings.UnlockDeviceBeforeLaunch = value;
                OnPropertyChanged();
            }
        }
    }

    public int UnlockTimeout
    {
        get => DeviceSettings.UnlockTimeout;
        set
        {
            if (DeviceSettings.UnlockTimeout != value)
            {
                DeviceSettings.UnlockTimeout = value;
                OnPropertyChanged();
            }
        }
    }

    public string? UnlockCommands
    {
        get => DeviceSettings.UnlockCommands;
        set
        {
            if (DeviceSettings.UnlockCommands != value)
            {
                DeviceSettings.UnlockCommands = value;
                OnPropertyChanged();
            }
        }
    }


    public string? CustomArguments
    {
        get => DeviceSettings.CustomArguments;
        set
        {
            if (DeviceSettings.CustomArguments != value)
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
        get => DeviceSettings.DisableVideoForwarding;
        set
        {
            if (DeviceSettings.DisableVideoForwarding != value)
            {
                DeviceSettings.DisableVideoForwarding = value;
                OnPropertyChanged();
            }
        }
    }

    public int VideoCodec
    {
        get => DeviceSettings.VideoCodec;
        set
        {
            if (DeviceSettings.VideoCodec != value)
            {
                DeviceSettings.VideoCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VideoBitrate
    {
        get => DeviceSettings.VideoBitrate;
        set
        {
            if (DeviceSettings.VideoBitrate != value)
            {
                DeviceSettings.VideoBitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public string? FrameRate
    {
        get => DeviceSettings.FrameRate;
        set
        {
            if (DeviceSettings.FrameRate != value)
            {
                DeviceSettings.FrameRate = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Crop
    {
        get => DeviceSettings.Crop;
        set
        {
            if (DeviceSettings.Crop != value)
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
        get => DeviceSettings.IsVirtualDisplayEnabled;
        set
        {
            if (DeviceSettings.IsVirtualDisplayEnabled != value)
            {   
                DeviceSettings.IsVirtualDisplayEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public string? VirtualDisplaySize
    {
        get => DeviceSettings.VirtualDisplaySize;
        set
        {
            if (DeviceSettings.VirtualDisplaySize != value)
            {
                DeviceSettings.VirtualDisplaySize = value;
                OnPropertyChanged();
            }
        }
    }

    public int DisplayOrientation
    {
        get => DeviceSettings.DisplayOrientation;
        set
        {
            if (DeviceSettings.DisplayOrientation != value)
            {
                DeviceSettings.DisplayOrientation = value;
                OnPropertyChanged();
            }
        }
    }

    public string? RotationAngle
    {
        get => DeviceSettings.RotationAngle;
        set
        {
            if (DeviceSettings.RotationAngle != value)
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
        get => DeviceSettings.AudioOutputMode;
        set
        {
            if (DeviceSettings.AudioOutputMode != value)
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
        get => DeviceSettings.AudioBitrate;
        set
        {
            if (DeviceSettings.AudioBitrate != value)
            {
                DeviceSettings.AudioBitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ForwardMicrophone
    {
        get => DeviceSettings.ForwardMicrophone;
        set
        {
            if (DeviceSettings.ForwardMicrophone != value)
            {
                DeviceSettings.ForwardMicrophone = value;
                OnPropertyChanged();
            }
        }
    }

    public int AudioCodec
    {
        get => DeviceSettings.AudioCodec;
        set
        {
            if (DeviceSettings.AudioCodec != value)
            {
                DeviceSettings.AudioCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string? AudioOutputBuffer
    {
        get => DeviceSettings.AudioOutputBuffer;
        set
        {
            if (DeviceSettings.AudioOutputBuffer != value)
            {
                DeviceSettings.AudioOutputBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    public string? AudioBuffer
    {
        get => DeviceSettings.AudioBuffer;
        set
        {
            if (DeviceSettings.AudioBuffer != value)
            {
                DeviceSettings.AudioBuffer = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #endregion

    #region Media Session Settings

    public bool MediaSession
    {
        get => DeviceSettings.MediaSession;
        set
        {
            if (DeviceSettings.MediaSession != value)
            {
                DeviceSettings.MediaSession = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AudioSync
    {
        get => DeviceSettings.AudioSync;
        set
        {
            if (DeviceSettings.AudioSync != value)
            {
                DeviceSettings.AudioSync = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region ADB Settings

    public bool AdbTcpipModeEnabled
    {
        get => DeviceSettings.AdbTcpipModeEnabled;
        set
        {
            if (DeviceSettings.AdbTcpipModeEnabled != value)
            {
                DeviceSettings.AdbTcpipModeEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AdbAutoConnect
    {
        get => DeviceSettings.AdbAutoConnect;
        set
        {
            if (DeviceSettings.AdbAutoConnect != value)
            {
                DeviceSettings.AdbAutoConnect = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Storage Access Settings

    public bool StorageAccess
    {
        get => DeviceSettings.StorageAccess;
        set
        {
            if (DeviceSettings.StorageAccess != value)
            {
                DeviceSettings.StorageAccess = value;
                OnPropertyChanged();
                
                // If storage access is disabled, remove the sync root
                if (!value)
                {
                    sftpService.Remove(Device.Id);
                }
            }
        }
    }

    #endregion

    private readonly ISftpService sftpService = Ioc.Default.GetRequiredService<ISftpService>();
    private readonly IAdbService AdbService = Ioc.Default.GetRequiredService<IAdbService>();
    private readonly IDeviceSettingsService DeviceSettings;
    public PairedDevice Device;

    private readonly RemoteAppRepository RemoteAppsRepository = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    public ObservableCollection<ApplicationInfo> RemoteApps { get; set; } = [];

    private readonly DeviceRepository DeviceRepository = Ioc.Default.GetRequiredService<DeviceRepository>();

    private bool isDragging = true;
    private bool isBulkOperation;

    public ObservableCollection<AddressEntry> Addresses { get; set; } = [];

    private string newAddress = string.Empty;
    public string NewAddress
    {
        get => newAddress;
        set => SetProperty(ref newAddress, value);
    }

    public bool CanRemoveAddress => Addresses.Count > 1;


    public DeviceSettingsViewModel(PairedDevice device)
    {
        Device = device;
        DeviceSettings = device.DeviceSettings;
        OnPropertyChanged(nameof(DeviceSettings));

        selectedAudioOutputMode = AudioOutputModeOptions[AudioOutputMode];
        selectedScrcpyDevicePreference = ScrcpyDevicePreferenceOptions[ScrcpyDevicePreference];

        OnPropertyChanged(nameof(SelectedAudioOutputMode));
        OnPropertyChanged(nameof(SelectedScrcpyDevicePreference));
        LoadApps(device.Id);
        LoadAddresses();
        
        Addresses.CollectionChanged += Addresses_CollectionChanged;
    }

    private void LoadAddresses()
    {
        isBulkOperation = true;
        Addresses = Device.Addresses.ToObservableCollection();        
        isBulkOperation = false;
    }

    private void Addresses_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (isBulkOperation) return;
        // Reordering ListView has no events, but its collection is updated twice,
        if (isDragging)
        {
            isDragging = false;
            return;
        }
        isDragging = true;
        
        OnPropertyChanged(nameof(CanRemoveAddress));
        SaveAddresses();
    }

    public async void SaveAddresses()
    {
        // Update priorities based on current order
        for (int i = 0; i < Addresses.Count; i++)
        {
            Addresses[i].Priority = i;
        }

        // Update device's addresses
        Device.Addresses = Addresses.ToList();

        // Save to database
        var deviceEntity = await DeviceRepository.GetPairedDevice(Device.Id);
        if (deviceEntity is not null)
        {
            deviceEntity.Addresses = Addresses.ToList();
            DeviceRepository.AddOrUpdateRemoteDevice(deviceEntity);
        }

        OnPropertyChanged(nameof(DisplayAddresses));
    }

    [RelayCommand]
    private void AddAddress()
    {
        if (string.IsNullOrWhiteSpace(NewAddress))
            return;

        var address = NewAddress.Trim();
        
        isBulkOperation = true;
        var newEntry = new AddressEntry
        {
            Address = address,
            IsEnabled = true,
            Priority = Addresses.Count
        };

        Addresses.Add(newEntry);
        isBulkOperation = false;
        NewAddress = string.Empty;
        OnPropertyChanged(nameof(CanRemoveAddress));
        SaveAddresses();
    }
        

    [RelayCommand]
    private void RemoveAddress(AddressEntry entry)
    {
        isBulkOperation = true;
        Addresses.Remove(entry);
        isBulkOperation = false;
        OnPropertyChanged(nameof(CanRemoveAddress));
        SaveAddresses();
    }

    public void LoadApps(string id)
    {
        RemoteApps = RemoteAppsRepository.GetApplicationsForDevice(id);
    }

    public void ChangeNotificationFilter(string notificationFilter, string appPackage)
    {
        var filterKey = ApplicationInfo.NotificationFilterTypes.First(f => f.Value == notificationFilter).Key;
        RemoteAppsRepository.UpdateAppNotificationFilter(Device!.Id, appPackage, filterKey);
        var app = RemoteApps.First(p => p.PackageName == appPackage);
        app.DeviceInfo.Filter = filterKey;
        app.SelectedNotificationFilter = notificationFilter;
    }
}
