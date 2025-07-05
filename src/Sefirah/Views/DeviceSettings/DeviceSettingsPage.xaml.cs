using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.Data.Models;
using Sefirah.ViewModels.Settings;
using Sefirah.Views.DeviceSettings;

namespace Sefirah.Views.DevicePreferences;

public sealed partial class DeviceSettingsPage : Page
{
    public DeviceSettingsViewModel ViewModel { get; }

    public DeviceSettingsPage()
    {
        this.InitializeComponent();
        ViewModel = new DeviceSettingsViewModel();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is PairedDevice device)
        {
            ViewModel.SetDevice(device);
        }
    }

    private void OpenNotificationsSettings(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(NotificationSettingsPage), ViewModel, new DrillInNavigationTransitionInfo());
    }

    private void OpenClipboardSettings(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ClipboardSettingsPage), ViewModel, new DrillInNavigationTransitionInfo());
    }

    private void OpenScreenMirrorSettings(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ScreenMirrorSettingsPage), ViewModel, new DrillInNavigationTransitionInfo());
    }

    private void OpenAdbSettings(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(AdbSettingsPage), ViewModel, new DrillInNavigationTransitionInfo());
    }
}
