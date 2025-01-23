using Microsoft.UI.Xaml.Controls;

namespace Sefirah.App.UserControls;

[ObservableObject]
public sealed partial class TrayIconControl : UserControl
{
    [ObservableProperty]
    private bool _isWindowVisible;

    public TrayIconControl()
    {
        this.InitializeComponent();
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
            window.AppWindow.Show();
        }
        IsWindowVisible = window.Visible;
    }

    [RelayCommand]
    public void ExitApplication()
    {
        var window = MainWindow.Instance;
        App.HandleClosedEvents = false;
        TrayIcon.Dispose();
        window.Close();
        App.Current.Exit();
    }
}
