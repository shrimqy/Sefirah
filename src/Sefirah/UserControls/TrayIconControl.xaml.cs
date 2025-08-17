using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Imaging;
#if WINDOWS
using Sefirah.Platforms.Windows.Interop;
#endif
using Windows.UI.ViewManagement;

namespace Sefirah.UserControls;
public sealed partial class TrayIconControl : UserControl
{
    private readonly UISettings uiSettings = new();
    public TrayIconControl()
    {
        InitializeComponent();

        // Set initial icon
        UpdateTrayIcon(uiSettings, null);

        // Monitor system theme changes
        uiSettings.ColorValuesChanged += UpdateTrayIcon;
    }

    [RelayCommand]
    public void ShowHideWindow()
    {
        var window = App.MainWindow;

        // Ensure window and AppWindow are not null
        if (window == null || window.AppWindow == null)
        {
            return;
        }
        if (window.Visible)
        {
            window.AppWindow.Hide();
        }
        else
        {
            window.AppWindow.Show();
#if WINDOWS
            InteropHelpers.SetForegroundWindow(App.WindowHandle);
#endif
        }
    }


    private void UpdateTrayIcon(UISettings sender, object args)
    {
        try
        {
            var iconPath = sender.GetColorValue(UIColorType.Background) == Colors.Black
                ? "ms-appx:///Assets/Icons/SefirahDark.ico"
                : "ms-appx:///Assets/Icons/SefirahLight.ico";

            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var imageSource = new BitmapImage
                    {
                        UriSource = new Uri(iconPath)
                    };
                    TrayIcon.IconSource = imageSource;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
                }
            });
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
