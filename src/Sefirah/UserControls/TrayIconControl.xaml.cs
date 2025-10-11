using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
#if WINDOWS
using Sefirah.Platforms.Windows.Interop;
#endif
using Windows.UI.ViewManagement;

namespace Sefirah.UserControls;
public sealed partial class TrayIconControl : UserControl
{
    private readonly UISettings uiSettings = new();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    public PairedDevice? Device => DeviceManager.ActiveDevice;
    public TrayIconControl()
    {
        InitializeComponent();

        // Set initial icon
        UpdateTrayIcon(uiSettings);

        // Monitor system theme changes
        uiSettings.ColorValuesChanged += UpdateTrayIcon;
    }

    [RelayCommand]
    public void ShowHideWindow()
    {
#if WINDOWS
        var window = App.MainWindow;
        if (window.Visible)
        {
            window.AppWindow.Hide();
        }
        else
        {
            window.AppWindow.Show();
            InteropHelpers.SetForegroundWindow(App.WindowHandle);
        }
#endif
    }

    [RelayCommand]
    public void StartScrcpy()
    {
        if (Device != null)
        {
            ScreenMirrorService.StartScrcpy(Device);
        }
    }

    private void UpdateTrayIcon(UISettings sender, object? args = null)
    {
        try
        {
            var iconPath = sender.GetColorValue(UIColorType.Background) == Colors.Black
                ? "ms-appx:///Assets/Icons/SefirahDark.ico"
                : "ms-appx:///Assets/Icons/SefirahLight.ico";

            DispatcherQueue.EnqueueAsync(() => TrayIcon.IconSource = new BitmapImage(new(iconPath)));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to detect theme: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ExitApplication()
    {
        App.HandleClosedEvents = false;
        TrayIcon.Dispose();

        // Close window and exit app
        App.MainWindow?.Close();
        App.Current.Exit();

        // Force termination if still needed
        Process.GetCurrentProcess().Kill();
    }
}
