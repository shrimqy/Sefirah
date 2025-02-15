using Microsoft.UI.Dispatching;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Extensions;

namespace Sefirah.App.ViewModels.Settings;

public sealed partial class GeneralViewModel : ObservableObject
{
    private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
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
        { Theme.Default, "ThemeDefault/Content".GetLocalizedResource() },
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
        { StartupOptions.Minimized, "StartupOptionMinimized/Content".GetLocalizedResource() },
        { StartupOptions.InTray, "StartupOptionSystemTray/Content".GetLocalizedResource() },
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

    public GeneralViewModel()
    {
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        selectedThemeType = ThemeTypes[CurrentTheme];
        selectedStartupType = StartupTypes[StartupOption];
    }
}
