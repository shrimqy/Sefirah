using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Extensions;
using Sefirah.App.Services;
using System.Threading.Tasks;

namespace Sefirah.App.ViewModels.Settings;

public sealed partial class GeneralViewModel : ObservableObject
{
    private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
    private readonly IDeviceManager _deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    private readonly DispatcherQueue dispatcherQueue;

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
                Task.Run(async () =>
                {
                    if (localDevice != null)
                    {
                        localDevice.DeviceName = value;
                        await _deviceManager.UpdateLocalDevice(localDevice);
                    }
                });
            }
        }
    }

    public GeneralViewModel()
    {
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        selectedThemeType = ThemeTypes[CurrentTheme];
        selectedStartupType = StartupTypes[StartupOption];

        // Load initial local device name
        LoadLocalDeviceName();
    }

    private async void LoadLocalDeviceName()
    {
        await dispatcherQueue.EnqueueAsync(async () =>
        {
            localDevice = await _deviceManager.GetLocalDeviceAsync();
            _localDeviceName = localDevice.DeviceName;
        });
    }
}
