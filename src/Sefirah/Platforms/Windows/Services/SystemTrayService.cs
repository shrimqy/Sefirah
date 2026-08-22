using System.Drawing;
using DesktopFlyouts;
using Microsoft.UI.Windowing;
using Sefirah.Platforms.Windows.Interop;
using Windows.UI.ViewManagement;

namespace Sefirah.Platforms.Windows.Services;

public sealed partial class SystemTrayService : ISystemTrayService
{
    private const string DarkTrayIconName = "SefirahDark.ico";
    private const string LightTrayIconName = "SefirahLight.ico";
    private static readonly Guid TrayIconId = new("6B3A1F2E-9C4D-4E5F-8A0B-1D2E3F4A5B6C");

    private readonly ILogger logger;
    private readonly UISettings uiSettings = new();

    private SystemTrayIcon? trayIcon;
    private TrayFlyout? trayFlyout;
    private TrayContextMenu? contextMenu;

    public bool IsAvailable { get; private set; }

    public SystemTrayService(ILogger logger)
    {
        this.logger = logger;

        var iconPath = GetTrayIconPath();
        try
        {
            trayIcon = new SystemTrayIcon(iconPath, "Sefirah", TrayIconId);
            trayIcon.Show();

            IsAvailable = trayIcon.IsVisible;
            trayFlyout = new TrayFlyout();
            contextMenu = new TrayContextMenu(ToggleMainWindowVisibility, ExitApplication);

            trayIcon.LeftClicked += OnTrayIconLeftClicked;
            trayIcon.RightClicked += OnTrayIconRightClicked;
            trayIcon.LeftDoubleClicked += OnTrayIconDoubleClicked;
            uiSettings.ColorValuesChanged += OnThemeChanged;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            logger.Error("Failed to initialize system tray icon", ex);
            Dispose();
        }
    }

    public void Dispose()
    {
        uiSettings.ColorValuesChanged -= OnThemeChanged;

        trayIcon?.LeftClicked -= OnTrayIconLeftClicked;
        trayIcon?.RightClicked -= OnTrayIconRightClicked;
        trayIcon?.LeftDoubleClicked -= OnTrayIconDoubleClicked;
        trayIcon?.Destroy();
        trayIcon = null;

        trayFlyout?.Dispose();
        trayFlyout = null;
        contextMenu?.Dispose();
        contextMenu = null;
        IsAvailable = false;
    }

    private void OnTrayIconLeftClicked(object? sender, MouseEventReceivedEventArgs e)
        => App.MainWindow.DispatcherQueue.TryEnqueue(ToggleTrayFlyout);

    private void OnTrayIconDoubleClicked(object? sender, MouseEventReceivedEventArgs e)
        => App.MainWindow.DispatcherQueue.TryEnqueue(ToggleMainWindowVisibility);

    private void OnTrayIconRightClicked(object? sender, MouseEventReceivedEventArgs e)
        => App.MainWindow.DispatcherQueue.TryEnqueue(() => ShowContextMenu(e.Point));

    private void OnThemeChanged(UISettings sender, object args)
        => App.MainWindow.DispatcherQueue.TryEnqueue(ApplyTraySystemTheme);

    private void ApplyTraySystemTheme()
    {
        if (trayIcon is null) return;

        try
        {
            var iconPath = GetTrayIconPath();
            logger.Debug($"Applying tray theme icon: {iconPath}");
            trayIcon.SetIcon(iconPath);
            trayFlyout?.ApplySystemTheme();
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to update tray icon theme", ex);
        }
    }

    private void ToggleTrayFlyout()
    {
        if (trayFlyout is null) return;

        if (trayFlyout.IsOpen)
            trayFlyout.Hide();
        else
            trayFlyout.Show();
    }

    private void ShowContextMenu(Point point)
    {
        if (contextMenu is null) return;

        if (contextMenu.IsOpen)
            contextMenu.Hide();

        contextMenu.Show(point);
    }

    private static string GetTrayIconPath()
    {
        var iconName = Helpers.SystemThemeHelper.SystemUsesLightTheme() ? LightTrayIconName : DarkTrayIconName;
        return Path.GetFullPath(Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Icons", iconName));
    }

    private static void ToggleMainWindowVisibility()
    {            
        var window = App.MainWindow;
        var presenter = window.AppWindow.Presenter as OverlappedPresenter;
        var isMinimized = presenter?.State is OverlappedPresenterState.Minimized;

        if (!window.Visible || isMinimized)
        {
            if (isMinimized && presenter is not null)
                presenter.Restore();

            window.AppWindow.Show();
            window.Activate();
            InteropHelpers.SetForegroundWindow(App.WindowHandle);
            return;
        }

        window.AppWindow.Hide();
    }

    private static void ExitApplication()
    {
        App.HandleClosedEvents = false;
        Ioc.Default.GetRequiredService<ISystemTrayService>().Dispose();

        App.MainWindow?.Close();
        App.Current.Exit();
        Process.GetCurrentProcess().Kill();
    }
}
