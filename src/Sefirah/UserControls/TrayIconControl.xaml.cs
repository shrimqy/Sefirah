namespace Sefirah.UserControls;
public sealed partial class TrayIconControl : UserControl
{
    public TrayIconControl()
    {
        InitializeComponent();
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
            window.Activate();
            window.AppWindow.Show();
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
