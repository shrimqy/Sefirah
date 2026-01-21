using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.ViewModels.Settings;
using Sefirah.Views;
using Sefirah.Views.DeviceSettings;

namespace Sefirah.Views.DevicePreferences;

public sealed partial class DeviceSettingsPage : Page
{
    public DeviceSettingsViewModel ViewModel { get; set; } = null!;

    public DeviceSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is DeviceSettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
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

    private void OpenAddressSettings(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(AddressesSettingsPage), ViewModel, new DrillInNavigationTransitionInfo());
    }

    private bool isUpdatingSelection = false;

    private void ClipboardSyncSegmented_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Segmented segmented)
        {
            UpdateSegmentedSelection(segmented);
        }
    }

    private void ClipboardSyncSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingSelection || sender is not Segmented segmented)
            return;

        // Index 0 = Receive (Download), Index 1 = Send (Upload)
        var selectedItems = segmented.SelectedItems;
        ViewModel.ClipboardReceive = segmented.Items.Count > 0 && selectedItems.Contains(segmented.Items[0]);
        ViewModel.ClipboardSend = segmented.Items.Count > 1 && selectedItems.Contains(segmented.Items[1]);
    }

    private void UpdateSegmentedSelection(Segmented segmented)
    {
        isUpdatingSelection = true;
        segmented.SelectedItems.Clear();
        
        if (ViewModel.ClipboardReceive && segmented.Items.Count > 0)
            segmented.SelectedItems.Add(segmented.Items[0]);
        
        if (ViewModel.ClipboardSend && segmented.Items.Count > 1)
            segmented.SelectedItems.Add(segmented.Items[1]);
        
        isUpdatingSelection = false;
    }
}
