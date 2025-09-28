using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.Data.Models;
using Sefirah.ViewModels;
using Sefirah.ViewModels.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Sefirah.Views;
public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; }
    public DevicesViewModel DevicesViewModel { get; }

    public MainPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<MainPageViewModel>();
        DevicesViewModel = Ioc.Default.GetRequiredService<DevicesViewModel>();
    }

    private readonly Dictionary<string, Type> Pages = new()
    {
        { "Settings", typeof(SettingsPage) },
        { "Messages", typeof(MessagesPage) },
        { "Apps", typeof(AppsPage) }
    };

    // Track the current animation to prevent conflicts
    private Storyboard? currentOverlayAnimation;

    // Handle mouse wheel events on the phone frame
    private void PhoneFrame_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Get the wheel delta - positive for scrolling up, negative for scrolling down
        var pointerPoint = e.GetCurrentPoint(PhoneFrameGrid);
        int wheelDelta = pointerPoint.Properties.MouseWheelDelta;        
        ViewModel.SwitchToNextDevice(wheelDelta);
        e.Handled = true;
    }

    private void NavigationView_SelectionChanged(NavigationView _, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem selectedItem && 
            selectedItem.Tag?.ToString() is string tag &&
            Pages.TryGetValue(tag, out Type? pageType))
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border &&
            border.FindName("PinIcon") is SymbolIcon pinIcon &&
            border.FindName("CloseButton") is Button closeButton &&
            border.FindName("MoreButton") is Button moreButton &&
            border.FindName("TimeStampTextBlock") is TextBlock timeStamp)
        {
            timeStamp.Visibility = Visibility.Collapsed;

            // Only make pinIcon visible if it's not already visible
            if (pinIcon.Tag is bool isPinned && isPinned)
            {
                pinIcon.Visibility = Visibility.Collapsed;
            }

            pinIcon.IsHitTestVisible = true;
            closeButton.Opacity = 1;
            closeButton.IsHitTestVisible = true;
            moreButton.Opacity = 1;
            moreButton.IsHitTestVisible = true;
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Check if MoreButton or its Flyout is open
        if (sender is Border border &&
            border.FindName("PinIcon") is SymbolIcon pinIcon &&
            border.FindName("CloseButton") is Button closeButton &&
            border.FindName("MoreButton") is Button moreButton &&
            border.FindName("TimeStampTextBlock") is TextBlock timeStamp)
        {
            // If the flyout is open, don't hide the buttons
            if (moreButton.Flyout is MenuFlyout flyout && flyout.IsOpen) return;

            timeStamp.Visibility = Visibility.Visible;

            if (pinIcon.Tag is bool isPinned && isPinned)
            {
                pinIcon.Visibility = Visibility.Visible;
            }

            pinIcon.IsHitTestVisible = false;
            closeButton.Opacity = 0;
            closeButton.IsHitTestVisible = false;
            moreButton.Opacity = 0;
            moreButton.IsHitTestVisible = false;
        }
    }

    private void MoreButtonFlyoutClosed(object sender, object e)
    {
        // The sender is the Flyout itself, so first get its parent button
        if (sender is MenuFlyout flyout && flyout.Target is Button moreButton && 
                VisualTreeHelper.GetParent(moreButton) is FrameworkElement parent &&
                parent.FindName("PinIcon") is SymbolIcon pinIcon &&
                parent.FindName("CloseButton") is Button closeButton &&
                parent.FindName("TimeStampTextBlock") is TextBlock timeStamp)
        {
            if (pinIcon.Tag is bool isPinned && isPinned)
            {
                pinIcon.Visibility = Visibility.Visible;
            }

            closeButton.Opacity = 0;
            closeButton.IsHitTestVisible = false;
            moreButton.Opacity = 0;
            moreButton.IsHitTestVisible = false;
            timeStamp.Visibility = Visibility.Visible;
        }
    }

    private async void OpenAppClick(object sender, RoutedEventArgs e)
    {   
        if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Notification notification)
        {
            await ViewModel.OpenApp(notification);
        }
    }

    private void UpdateNotificationFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string appPackage)
        {
            ViewModel.UpdateNotificationFilter(appPackage);
        }
    }

    private void ToggleNotificationPinClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Notification notification)
        {
            ViewModel.ToggleNotificationPin(notification);
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is Notification notification &&
            (button.Parent as FrameworkElement)?.FindName("ReplyTextBox") is TextBox replyTextBox)
        {
            string replyText = replyTextBox.Text;
            // Clear the textbox after getting the text
            replyTextBox.Text = string.Empty;

            ViewModel.HandleNotificationReply(notification, replyText);
        }
    }

    private void ReplyTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is TextBox textBox &&
            e.Key == VirtualKey.Enter &&
            textBox.Tag is Notification message)
        {
            ViewModel.HandleNotificationReply(message, textBox.Text);
            textBox.Text = string.Empty;
        }
    }

    private void PhoneFrame_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimateOverlay(PhoneFrameOverlay, true);
    }

    private void PhoneFrame_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimateOverlay(PhoneFrameOverlay, false);
    }

    private void AnimateOverlay(UIElement overlay, bool show)
    {
        // Cancel any existing animation to prevent conflicts
        currentOverlayAnimation?.Stop();
        currentOverlayAnimation = null;

        if (show)
        {
            overlay.Visibility = Visibility.Visible;
            currentOverlayAnimation = FadeInStoryboard;
            FadeInStoryboard.Begin();
        }
        else
        {
            currentOverlayAnimation = FadeOutStoryboard;
            FadeOutStoryboard.Begin();
            
            // Hide overlay after animation completes
            FadeOutStoryboard.Completed += (s, args) => 
            {
                overlay.Visibility = Visibility.Collapsed;
                currentOverlayAnimation = null;
            };
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        // Check if the dropped data contains files
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            ViewModel.SendFiles(await e.DataView.GetStorageItemsAsync());
        }
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (ViewModel.PairedDevices.Count == 0) return;

        e.AcceptedOperation = DataPackageOperation.Copy;
        if (ViewModel.PairedDevices.Count == 1)
        {
            e.DragUIOverride.Caption = $"Send to {ViewModel.Device?.Name}";
        }
    }
}
