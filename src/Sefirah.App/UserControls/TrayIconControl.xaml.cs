using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.ViewManagement;

namespace Sefirah.App.UserControls;

[ObservableObject]
public sealed partial class TrayIconControl : UserControl
{
    private readonly UISettings uiSettings = new();

    public TrayIconControl()
    {
        this.InitializeComponent();
        
        // Set initial icon
        UpdateTrayIcon(uiSettings, null);
        
        // Monitor system theme changes
        uiSettings.ColorValuesChanged += UpdateTrayIcon;
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
                    // Log or handle the image loading error
                    Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            // Log or handle the theme detection error
            Debug.WriteLine($"Failed to detect theme: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ShowHideWindow()
    {
        var window = MainWindow.Instance;
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
            window.Activate();
            window.AppWindow.Show();
        }
    }

    [RelayCommand]
    public void ExitApplication()
    {
        App.HandleClosedEvents = false;
        TrayIcon.Dispose();

        MainWindow.Instance.Close();
        App.Current.Exit();

        // Force termination if still needed
        Process.GetCurrentProcess().Kill();
    }
}
