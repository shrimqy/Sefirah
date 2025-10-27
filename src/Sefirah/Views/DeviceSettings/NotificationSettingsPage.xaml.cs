using Sefirah.ViewModels.Settings;

namespace Sefirah.Views.DeviceSettings;

public sealed partial class NotificationSettingsPage : Page
{
    public DeviceSettingsViewModel ViewModel
    {
        get => (DeviceSettingsViewModel)DataContext;
        private set => DataContext = value;
    }

    public NotificationSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is DeviceSettingsViewModel viewModel)
        {
            ViewModel = viewModel;
        }
    }

    public void OnMenuFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem &&
            menuItem.Tag is string appPackage)
        {
            ViewModel.ChangeNotificationFilter(menuItem.Text, appPackage);
        }
    }
}
