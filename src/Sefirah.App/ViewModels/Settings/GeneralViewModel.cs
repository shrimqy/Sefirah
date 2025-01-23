using Microsoft.UI.Dispatching;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Windows.ApplicationModel;

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
        { Theme.Default, "System Default" },
        { Theme.Light, "Light" },
        { Theme.Dark, "Dark" }
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
                CurrentTheme = newTheme; // Direct assignment, no dispatcher needed
            }
        }
    }

    // Startup Options settings
    public StartupOptions StartupOption
    {
        get => UserSettingsService.GeneralSettingsService.StartupOption;
        set
        {
            if (value != UserSettingsService.GeneralSettingsService.StartupOption)
            {
                UserSettingsService.GeneralSettingsService.StartupOption = value;
                // Update startup task when option changes
                _ = HandleStartupTaskAsync(value != StartupOptions.Disabled);
                OnPropertyChanged();
            }
        }
    }

    public Dictionary<StartupOptions, string> StartupTypes { get; } = new()
    {
        { StartupOptions.Disabled, "Disabled" },
        { StartupOptions.Minimized, "Start Minimized" },
        { StartupOptions.InTray, "Start in System Tray" },
        { StartupOptions.Maximized, "Start Maximized" }
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

    private static async Task HandleStartupTaskAsync(bool enable)
    {
        var startupTask = await StartupTask.GetAsync("8B5D3E3F-9B69-4E8A-A9F7-BFCA793B9AF0");
        
        if (enable)
        {
            if (startupTask.State == StartupTaskState.Disabled)
                await startupTask.RequestEnableAsync();
        }
        else
        {
            if (startupTask.State == StartupTaskState.Enabled)
                startupTask.Disable();
        }
    }

    public GeneralViewModel()
    {
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Initialize selected theme
        selectedThemeType = ThemeTypes[CurrentTheme];
        
        // Initialize selected startup option
        selectedStartupType = StartupTypes[StartupOption];
    }
}
