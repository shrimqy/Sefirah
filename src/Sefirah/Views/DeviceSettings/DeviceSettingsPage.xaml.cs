using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.ViewModels.Settings;
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

    private bool isUpdatingClipboardSelection = false;
    private bool isUpdatingMediaSessionSelection = false;

    private void ClipboardSyncSegmented_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Segmented segmented)
            return;

        isUpdatingClipboardSelection = true;
        try
        {
            ApplySegmentedMultiSelection(segmented, ViewModel.ClipboardReceive, ViewModel.ClipboardSend);
        }
        finally
        {
            isUpdatingClipboardSelection = false;
        }
    }

    private void ClipboardSyncSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingClipboardSelection || sender is not Segmented segmented)
            return;

        ViewModel.ClipboardReceive = IsSegmentedIndexSelected(segmented, 0);
        ViewModel.ClipboardSend = IsSegmentedIndexSelected(segmented, 1);
    }

    private void MediaSessionSyncSegmented_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Segmented segmented)
            return;

        isUpdatingMediaSessionSelection = true;
        try
        {
            ApplySegmentedMultiSelection(segmented, ViewModel.MediaSessionReceive, ViewModel.MediaSessionSend);
        }
        finally
        {
            isUpdatingMediaSessionSelection = false;
        }
    }

    private void MediaSessionSyncSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingMediaSessionSelection || sender is not Segmented segmented)
            return;

        ViewModel.MediaSessionReceive = IsSegmentedIndexSelected(segmented, 0);
        ViewModel.MediaSessionSend = IsSegmentedIndexSelected(segmented, 1);
    }

    /// Sets selection via realized <see cref="SelectorItem"/> containers instead of
    /// <see cref="ListViewBase.SelectedItems"/>. Adding <see cref="ItemCollection"/> entries there
    /// can throw <see cref="InvalidCastException"/>
    private static void ApplySegmentedMultiSelection(Segmented segmented, bool selectFirst, bool selectSecond)
    {
        for (var i = 0; i < segmented.Items.Count; i++)
        {
            if (segmented.ContainerFromIndex(i) is not SelectorItem selectorItem) continue;

            var selected = (i == 0 && selectFirst) || (i == 1 && selectSecond);
            selectorItem.IsSelected = selected;
        }
    }

    private static bool IsSegmentedIndexSelected(Segmented segmented, int index) =>
        segmented.ContainerFromIndex(index) is SelectorItem { IsSelected: true };
}
