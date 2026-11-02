using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Items;
using Sefirah.Helpers;

namespace Sefirah.ViewModels.Settings;
public sealed partial class GeneralViewModel : BaseViewModel
{
    #region Services
    private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
    private readonly IAppThemeModeService AppThemeModeService = Ioc.Default.GetRequiredService<IAppThemeModeService>();
    private readonly IDeviceManager _deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    private readonly IAdbService AdbService = Ioc.Default.GetRequiredService<IAdbService>();
    private readonly ISftpFeature SftpFeature = Ioc.Default.GetRequiredService<ISftpFeature>();
    #endregion

    private IGeneralSettingsService PresentationSettings => UserSettingsService.GeneralSettingsService;
    public string[] PresentationKinds => Sefirah.Data.Models.DevicePresentation.Kinds.Select(value => ("Presentation_" + value).GetLocalizedResource()).ToArray();
    public string[] PresentationRatios => Sefirah.Data.Models.DevicePresentation.Ratios;
    public string[] PresentationOrientations => new[] { "portrait", "landscape" }.Select(value => ("Presentation_" + value).GetLocalizedResource()).ToArray();
    public string PresentationKind { get => ("Presentation_" + PresentationSettings.PresentationKind).GetLocalizedResource(); set { PresentationSettings.PresentationKind = Sefirah.Data.Models.DevicePresentation.Kinds.FirstOrDefault(kind => ("Presentation_" + kind).GetLocalizedResource() == value) ?? "unknown"; OnPropertyChanged(); PublishPresentation(); } }
    public string PresentationRatio { get => PresentationSettings.PresentationRatio; set { PresentationSettings.PresentationRatio = value; OnPropertyChanged(); PublishPresentation(); } }
    public string PresentationOrientation { get => ("Presentation_" + PresentationSettings.PresentationOrientation).GetLocalizedResource(); set { PresentationSettings.PresentationOrientation = new[] { "portrait", "landscape" }.FirstOrDefault(direction => ("Presentation_" + direction).GetLocalizedResource() == value) ?? "landscape"; OnPropertyChanged(); PublishPresentation(); } }
    public bool ShareWallpaperPreview { get => PresentationSettings.ShareWallpaperPreview; set { PresentationSettings.ShareWallpaperPreview = value; OnPropertyChanged(); PublishPresentation(); } }
    private Sefirah.Data.Models.DevicePresentation? localPresentation;
    public Sefirah.Data.Models.DevicePresentation? LocalPresentation { get => localPresentation; private set => SetProperty(ref localPresentation, value); }
    private Microsoft.UI.Xaml.Media.ImageSource? localPreviewImage;
    public Microsoft.UI.Xaml.Media.ImageSource? LocalPreviewImage { get => localPreviewImage; private set => SetProperty(ref localPreviewImage, value); }
    private async void PublishPresentation()
    {
        var service = Ioc.Default.GetRequiredService<Sefirah.Services.DevicePresentationService>();
        var preview = await service.GetAsync(null, true);
        LocalPresentation = preview.Metadata();
        LocalPreviewImage = await Sefirah.Utils.PresentationImage.Decode(preview.Wallpaper, preview.WallpaperId).ToBitmapAsync();
        await service.PublishAllAsync();
    }
    public void RefreshPresentationPreview() => PublishPresentation();
    [RelayCommand]
    public async Task SelectPresentationImage()
    {
        var file = await Sefirah.Utils.PickerHelper.PickFileAsync([".jpg", ".jpeg", ".png"]);
        if (file is null) return;
        var bytes = await Sefirah.Utils.WallpaperPreviewProvider.CreateThumbnailAsync(file.Path);
        if (bytes is null) return;
        var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "device-preview.jpg");
        await File.WriteAllBytesAsync(path, bytes);
        PresentationSettings.PresentationImagePath = path;
        PublishPresentation();
    }
    [RelayCommand]
    public void UseSystemWallpaper() { PresentationSettings.PresentationImagePath = string.Empty; PublishPresentation(); }

    #region Properties
    // Language
    public ObservableCollection<AppLanguageItem> SupportedLanguages => AppLanguageHelper.SupportedLanguages;

    private int selectedAppLanguageIndex;
    public int SelectedAppLanguageIndex
    {
        get => selectedAppLanguageIndex;
        set
        {
            if (AppLanguageHelper.TryChange(value))
            {
                selectedAppLanguageIndex = value;
                OnPropertyChanged(nameof(SelectedAppLanguageIndex));
            }
        }
    }

    // Theme settings
    public Theme CurrentTheme
    {
        get => AppThemeModeService.Theme;
        set
        {
            if (value != AppThemeModeService.Theme)
            {
                AppThemeModeService.Theme = value;
                OnPropertyChanged();
            }
        }
    }

    public Dictionary<Theme, string> ThemeTypes { get; } = new()
    {
        { Theme.Default, "Default".GetLocalizedResource() },
        { Theme.Light, "ThemeLight/Content".GetLocalizedResource() },
        { Theme.Dark, "ThemeDark/Content".GetLocalizedResource() }
    };

    private string selectedThemeType;
    public string SelectedThemeType
    {
        get => selectedThemeType;
        set
        {
            if (SetProperty(ref selectedThemeType, value))
            {
                var newTheme = ThemeTypes.First(t => t.Value == value).Key;
                CurrentTheme = newTheme;
            }
        }
    }

    public BackdropMaterialType BackdropMaterial
    {
        get => AppThemeModeService.BackdropMaterial;
        set
        {
            if (value != AppThemeModeService.BackdropMaterial)
            {
                AppThemeModeService.BackdropMaterial = value;
                OnPropertyChanged();
            }
        }
    }

    public Dictionary<BackdropMaterialType, string> BackdropMaterialTypes { get; } = new()
    {
        { BackdropMaterialType.Acrylic, "BackdropMaterialAcrylic".GetLocalizedResource() },
        { BackdropMaterialType.Mica, "BackdropMaterialMica".GetLocalizedResource() }
    };

    private string selectedBackdropMaterial = string.Empty;
    public string SelectedBackdropMaterial
    {
        get => selectedBackdropMaterial;
        set
        {
            if (SetProperty(ref selectedBackdropMaterial, value))
                BackdropMaterial = BackdropMaterialTypes.First(t => t.Value == value).Key;
        }
    }

    public StartupOptions StartupOption
    {
        get => UserSettingsService.GeneralSettingsService.StartupOption;
        set
        {
            if (value != UserSettingsService.GeneralSettingsService.StartupOption)
            {
                UserSettingsService.GeneralSettingsService.StartupOption = value;
                // Update startup task when option changes
                _ = AppLifecycleHelper.HandleStartupTaskAsync(value != StartupOptions.Disabled);
                OnPropertyChanged();
            }
        }
    }

    public Dictionary<StartupOptions, string> StartupTypes { get; } = new()
    {
        { StartupOptions.Disabled, "StartupOptionDisabled/Content".GetLocalizedResource() },
        { StartupOptions.InTray, "StartupOptionSystemTray/Content".GetLocalizedResource() },
        { StartupOptions.Minimized, "StartupOptionMinimized/Content".GetLocalizedResource() },
        { StartupOptions.Maximized, "StartupOptionMaximized/Content".GetLocalizedResource() }
    };

    private string selectedStartupType;
    public string SelectedStartupType
    {
        get => selectedStartupType;
        set
        {
            if (SetProperty(ref selectedStartupType, value))
            {
                StartupOption = StartupTypes.First(t => t.Value == value).Key;
            }
        }
    }

    private LocalDeviceEntity? localDevice;

    private string _localDeviceName = string.Empty;
    public string LocalDeviceName
    {
        get => _localDeviceName;
        set
        {
            if (SetProperty(ref _localDeviceName, value) && !string.IsNullOrWhiteSpace(value))
            {
                Task.Run(() =>
                {
                    if (localDevice != null)
                    {
                        localDevice.DeviceName = value;
                        _deviceManager.UpdateLocalDevice(localDevice);
                    }
                });
            }
        }
    }

    public string ScrcpyPath
    {
        get => UserSettingsService.GeneralSettingsService.ScrcpyPath;
        set
        {
            UserSettingsService.GeneralSettingsService.ScrcpyPath = value;
            OnPropertyChanged();
        }
    }

    public string AdbPath
    {
        get => UserSettingsService.GeneralSettingsService.AdbPath;
        set
        {
            UserSettingsService.GeneralSettingsService.AdbPath = value;
            OnPropertyChanged();
            AdbService.StartAsync();
        }
    }

    public string ReceivedFilesPath
    {
        get => UserSettingsService.GeneralSettingsService.ReceivedFilesPath;
        set
        {
            if (value != UserSettingsService.GeneralSettingsService.ReceivedFilesPath)
            {
                UserSettingsService.GeneralSettingsService.ReceivedFilesPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string RemoteStoragePath
    {
        get => UserSettingsService.GeneralSettingsService.RemoteStoragePath;
        set
        {
            // TODO : Delete the previous remote storage folder or move all the placeholders to the new location
            if (value != UserSettingsService.GeneralSettingsService.RemoteStoragePath)
            {
                UserSettingsService.GeneralSettingsService.RemoteStoragePath = value;
                OnPropertyChanged();
            }
        }
    }

    public StorageMountPreference StorageMountPreference
    {
        get => UserSettingsService.GeneralSettingsService.StorageMountPreference;
        set
        {
            if (value != UserSettingsService.GeneralSettingsService.StorageMountPreference)
            {
                UserSettingsService.GeneralSettingsService.StorageMountPreference = value;
                OnPropertyChanged();
            }
        }
    }

    public Dictionary<StorageMountPreference, string> StorageMountPreferenceTypes { get; } = new()
    {
        { StorageMountPreference.Auto, "Auto".GetLocalizedResource() },
        { StorageMountPreference.Gvfs, "GVfs".GetLocalizedResource() },
        { StorageMountPreference.Sshfs, "Sshfs".GetLocalizedResource() }
    };

    private string selectedStorageMountPreference = string.Empty;
    public string SelectedStorageMountPreference
    {
        get => selectedStorageMountPreference;
        set
        {
            if (SetProperty(ref selectedStorageMountPreference, value))
                StorageMountPreference = StorageMountPreferenceTypes.First(t => t.Value == value).Key;
        }
    }
    #endregion
    
    public GeneralViewModel()
    {
        selectedThemeType = ThemeTypes[CurrentTheme];
        selectedBackdropMaterial = BackdropMaterialTypes[BackdropMaterial];
        selectedStartupType = StartupTypes[StartupOption];
        selectedStorageMountPreference = StorageMountPreferenceTypes[StorageMountPreference];
        selectedAppLanguageIndex = AppLanguageHelper.SupportedLanguages.IndexOf(AppLanguageHelper.PreferredLanguage);
        // Load initial local device name
        LoadLocalDeviceName();
    }

    [RelayCommand]
    private async Task RemoveAllStorageMounts()
    {
        await SftpFeature.RemoveAll();
    }

    private void LoadLocalDeviceName()
    {
        dispatcher.EnqueueAsync(async () =>
        {
            localDevice = await _deviceManager.GetLocalDeviceAsync();
            LocalDeviceName = localDevice.DeviceName;
        });
    }
}
