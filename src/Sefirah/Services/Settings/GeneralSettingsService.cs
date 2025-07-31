using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Actions;
using Sefirah.Utils.Serialization;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Sefirah.Services.Settings;
internal sealed class GeneralSettingsService : BaseObservableJsonSettings, IGeneralSettingsService
{
    private readonly UISettings _uiSettings = new();
    private bool _isApplyingTheme;

    public event EventHandler? ThemeChanged;

    public GeneralSettingsService(ISettingsSharingContext settingsSharingContext)
    {
        // Register root
        RegisterSettingsContext(settingsSharingContext);

        // Listen for system theme changes
        _uiSettings.ColorValuesChanged += (s, e) =>
        {
            if (Theme == Theme.Default)
            {
                App.MainWindow?.DispatcherQueue.EnqueueAsync(() =>
                {
                    ApplyTheme(App.MainWindow, null, Theme.Default);
                });
            }
        };

        // Initialize theme
        ApplyTheme(App.MainWindow, null, Theme);
    }

    public StartupOptions StartupOption 
    { 
        get => Get(StartupOptions.InTray);
        set => Set(value);
    }

    public Theme Theme 
    { 
        get => Get(Theme.Default);
        set
        {
            if (Set(value))
            {
                ApplyTheme(App.MainWindow, null, value);
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void ApplyTheme(Window? window = null, AppWindowTitleBar? titleBar = null, Theme? theme = null, bool callThemeModeChangedEvent = true)
    {
        if (_isApplyingTheme) return;

        try
        {
            _isApplyingTheme = true;

            window ??= App.MainWindow;
            if (window?.Content == null) return;

            titleBar ??= window.AppWindow?.TitleBar;
            theme ??= Theme;

            // Update root element theme
            if (window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = theme switch
                {
                    Theme.Light => ElementTheme.Light,
                    Theme.Dark => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
#if WINDOWS
            // Update titlebar
            if (titleBar is not null)
            {
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                switch (theme)
                {
                    case Theme.Default:
                        titleBar.ButtonHoverBackgroundColor = (Color)Application.Current.Resources["SystemBaseLowColor"];
                        titleBar.ButtonForegroundColor = (Color)Application.Current.Resources["SystemBaseHighColor"];
                        break;
                    case Theme.Light:
                        titleBar.ButtonHoverBackgroundColor = Color.FromArgb(51, 0, 0, 0);
                        titleBar.ButtonForegroundColor = Colors.Black;
                        break;
                    case Theme.Dark:
                        titleBar.ButtonHoverBackgroundColor = Color.FromArgb(51, 255, 255, 255);
                        titleBar.ButtonForegroundColor = Colors.White;
                        break;
                }
            }
#endif
            if (callThemeModeChangedEvent)
                ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error applying theme: {ex}");
        }
        finally
        {
            _isApplyingTheme = false;
        }
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

    public string? ScrcpyPath
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public string? AdbPath
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public List<BaseAction> Actions
    {
        get => Get<List<BaseAction>>(new List<BaseAction>());
        set => Set(value);
    }

    public void AddAction(BaseAction action)
    {
        var actions = Actions.ToList();
        actions.Add(action);
        Actions = actions;
    }

    public void UpdateAction(BaseAction action)
    {
        var actions = Actions.ToList();
        var index = actions.FindIndex(a => a.Id == action.Id);
        if (index != -1)
        {
            actions.RemoveAt(index);
            actions.Insert(index, action);
            Actions = actions;
        }
    }

    public void RemoveAction(BaseAction action)
    {
        var actions = Actions.ToList();
        var index = actions.FindIndex(a => a.Id == action.Id);
        if (index != -1)
        {
            actions.RemoveAt(index);
            Actions = actions;
        }
    }
}
