using Sefirah.Data.Enums;
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
            menuItem.Tag is string appPackage &&
            TryGetNotificationFilter(menuItem.CommandParameter, out var filter))
        {
            ViewModel.ChangeNotificationFilter(filter, appPackage);
        }
    }

    public void OnSetAllMenuFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem &&
            TryGetNotificationFilter(menuItem.Tag, out var filter))
        {
            ViewModel.ChangeAllNotificationFilters(filter);
        }
    }

    private static bool TryGetNotificationFilter(object? filterValue, out NotificationFilter filter)
    {
        if (filterValue is string filterTag &&
            Enum.TryParse(filterTag, out filter))
        {
            return true;
        }

        filter = NotificationFilter.ToastFeed;
        return false;
    }
}
