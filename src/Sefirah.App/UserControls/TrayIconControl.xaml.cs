using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

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
        App.HandleClosedEvents = false;
        TrayIcon.Dispose();
       
        // Close window and exit app
        MainWindow.Instance.Close();
        App.Current.Exit();
        
        // Force termination if still needed
        Process.GetCurrentProcess().Kill();
    }
}
