using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Extensions;
using Sefirah.Helpers;

namespace Sefirah.ViewModels.Settings;
public sealed partial class GeneralViewModel : BaseViewModel
{
    #region Services
    private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
    private readonly IDeviceManager _deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    private readonly IAdbService AdbService = Ioc.Default.GetRequiredService<IAdbService>();
    #endregion

    #region Properties
    // Theme settings
    public Theme CurrentTheme
    {
        get => UserSettingsService.GeneralSettingsService.Theme;
        set
        {
            if (value != UserSettingsService.GeneralSettingsService.Theme)
            {
                UserSettingsService.GeneralSettingsService.Theme = value;
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

    public string? ScrcpyPath
    {
        get => UserSettingsService.GeneralSettingsService.ScrcpyPath;
        set
        {
            UserSettingsService.GeneralSettingsService.ScrcpyPath = value;
            OnPropertyChanged();
        }
    }

    public string? AdbPath
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
                var sftpService = Ioc.Default.GetRequiredService<ISftpService>();
                //sftpService.RemoveAllSyncRoots();
                OnPropertyChanged();
            }
        }
    }
    #endregion
    
    public GeneralViewModel()
    {
        selectedThemeType = ThemeTypes[CurrentTheme];
        selectedStartupType = StartupTypes[StartupOption];

        // Load initial local device name
        LoadLocalDeviceName();
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
