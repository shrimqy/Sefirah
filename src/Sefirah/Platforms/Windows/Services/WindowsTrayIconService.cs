using System.Drawing;
using DesktopFlyouts;
using Microsoft.UI.Windowing;
using Sefirah.Platforms.Windows.Interop;
using Windows.UI.ViewManagement;

namespace Sefirah.Platforms.Windows.Services;

public sealed partial class WindowsTrayIconService : ISystemTrayService
{
    private const string DarkTrayIconName = "SefirahDark.ico";
    private const string LightTrayIconName = "SefirahLight.ico";
    private static readonly Guid TrayIconId = new("6B3A1F2E-9C4D-4E5F-8A0B-1D2E3F4A5B6C");

    private readonly SystemTrayIcon trayIcon;
    private readonly UISettings uiSettings = new();

    private readonly TrayFlyout trayFlyout;
    private readonly TrayContextMenu contextMenu;

    public bool IsAvailable => true;

    public WindowsTrayIconService()
    {
        trayIcon = new SystemTrayIcon(GetTrayIconPath(), "Sefirah", TrayIconId);
        trayIcon.Show();

        trayFlyout = new TrayFlyout();
        contextMenu = new TrayContextMenu(ToggleMainWindowVisibility, ExitApplication);

        trayIcon.LeftClicked += OnTrayIconLeftClicked;
        trayIcon.RightClicked += OnTrayIconRightClicked;
        trayIcon.LeftDoubleClicked += OnTrayIconDoubleClicked;
        uiSettings.ColorValuesChanged += OnThemeChanged;
    }

    public void Dispose()
    {
        uiSettings.ColorValuesChanged -= OnThemeChanged;
        trayIcon.LeftClicked -= OnTrayIconLeftClicked;
        trayIcon.RightClicked -= OnTrayIconRightClicked;
        trayIcon.LeftDoubleClicked -= OnTrayIconDoubleClicked;
        trayFlyout.Dispose();
        contextMenu.Dispose();
        trayIcon.Destroy();
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
        trayIcon.SetIcon(GetTrayIconPath());
        trayFlyout.ApplySystemTheme();
    }

    private void ToggleTrayFlyout()
    {
        if (trayFlyout.IsOpen)
            trayFlyout.Hide();
        else
            trayFlyout.Show();
    }

    private void ShowContextMenu(Point point)
    {
        if (contextMenu.IsOpen)
            contextMenu.Hide();

        contextMenu.Show(point);
    }

    private static string GetTrayIconPath()
    {
        var iconName = Helpers.SystemThemeHelper.SystemUsesLightTheme() ? LightTrayIconName : DarkTrayIconName;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", iconName));
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
