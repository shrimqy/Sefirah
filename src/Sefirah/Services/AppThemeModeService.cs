using CommunityToolkit.WinUI;
using Microsoft.UI;
#if WINDOWS
using Microsoft.UI.Xaml.Media;
#endif
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Sefirah.Services;

public sealed class AppThemeModeService : IAppThemeModeService
{
    private readonly IUserSettingsService _userSettingsService;
    private readonly UISettings _uiSettings = new();

    public event EventHandler? AppThemeModeChanged;

    public event EventHandler? BackdropChanged;

    public AppThemeModeService(IUserSettingsService userSettingsService)
    {
        _userSettingsService = userSettingsService;
        _uiSettings.ColorValuesChanged += OnSystemThemeChanged;
    }

    public Theme Theme
    {
        get => _userSettingsService.GeneralSettingsService.Theme;
        set
        {
            var settings = _userSettingsService.GeneralSettingsService;
            if (settings.Theme == value)
                return;

            settings.Theme = value;
            AppThemeModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public BackdropMaterialType BackdropMaterial
    {
        get => _userSettingsService.GeneralSettingsService.BackdropMaterial;
        set
        {
            var settings = _userSettingsService.GeneralSettingsService;
            if (settings.BackdropMaterial == value)
                return;

            settings.BackdropMaterial = value;
            BackdropChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyBackdrop(Window window)
    {
#if WINDOWS
        window.SystemBackdrop = BackdropMaterial is BackdropMaterialType.Acrylic
            ? new DesktopAcrylicBackdrop()
            : new MicaBackdrop();
#endif
    }

    public void ManageAppearance(Window window)
    {
        SetAppThemeMode(window);
        ApplyBackdrop(window);

        AppThemeModeChanged += onThemeChanged;
        BackdropChanged += onBackdropChanged;
        window.Closed += OnWindowClosed;

        void onThemeChanged(object? sender, EventArgs args) => SetAppThemeMode(window);

        void onBackdropChanged(object? sender, EventArgs args) => ApplyBackdrop(window);

        void OnWindowClosed(object sender, WindowEventArgs args)
        {
            window.Closed -= OnWindowClosed;
            AppThemeModeChanged -= onThemeChanged;
            BackdropChanged -= onBackdropChanged;
        }
    }

    public void SetAppThemeMode(Window window)
    {
        try
        {
            if (window.Content is null)
                return;

            var theme = Theme;

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
            var titleBar = window.AppWindow?.TitleBar;
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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to change theme mode: {ex}");
        }
    }

    private void OnSystemThemeChanged(UISettings sender, object args)
    {
        if (Theme is not Theme.Default)
            return;

        App.MainWindow?.DispatcherQueue.EnqueueAsync(() =>
            AppThemeModeChanged?.Invoke(this, EventArgs.Empty));
    }
}
