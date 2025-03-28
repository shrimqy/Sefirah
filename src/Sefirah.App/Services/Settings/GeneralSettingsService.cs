using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Utils.Serialization;
using System.Runtime.InteropServices;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Sefirah.App.Services.Settings;
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
        _uiSettings.ColorValuesChanged += async (s, e) =>
        {
            if (Theme == Theme.Default)
            {
                await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                {
                    ApplyTheme(MainWindow.Instance, null, Theme.Default);
                });
            }
        };

        // Initialize theme
        ApplyTheme(MainWindow.Instance, null, Theme);
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
                ApplyTheme(MainWindow.Instance, null, value);
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void ApplyTheme(Window? window = null, AppWindowTitleBar? titleBar = null, Theme? theme = null)
    {
        if (_isApplyingTheme) return;

        try
        {
            _isApplyingTheme = true;

            window ??= MainWindow.Instance;
            if (window?.Content == null) return;

            titleBar ??= window.AppWindow?.TitleBar;
            theme ??= Theme;

            var isDark = theme == Theme.Dark || 
                (theme == Theme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);

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

            // Update titlebar
            if (titleBar is not null)
            {
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = isDark ? 
                    Color.FromArgb(51, 255, 255, 255) : 
                    Color.FromArgb(51, 0, 0, 0);
                titleBar.ButtonForegroundColor = isDark ? 
                    Colors.White : 
                    Colors.Black;

                // Update window icon
                try
                {
                    window.AppWindow?.SetIcon(isDark ? 
                        "Assets/SefirahDark.ico" : 
                        "Assets/SefirahLight.ico");
                }
                catch (COMException ex)
                {
                    Debug.WriteLine($"Failed to set window icon: {ex.Message}");
                }
            }
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
}
