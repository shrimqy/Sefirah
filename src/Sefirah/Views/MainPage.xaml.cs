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

    // Handle mouse wheel events on the phone frame
    private void PhoneFrame_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Get the wheel delta - positive for scrolling up, negative for scrolling down
        var pointerPoint = e.GetCurrentPoint(PhoneFrameGrid);
        int wheelDelta = pointerPoint.Properties.MouseWheelDelta;
        
        // Determine scroll direction (true for down, false for up)
        bool scrollDown = wheelDelta < 0;
        
        // Call the ViewModel to switch devices
        ViewModel.SwitchToNextDevice(scrollDown);
        
        // Mark the event as handled to prevent further processing
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
        var border = sender as Border;
        var pinIcon = FindChild<SymbolIcon>(border, "PinIcon");
        var closeButton = FindChild<Button>(border, "CloseButton");
        var moreButton = FindChild<Button>(border, "MoreButton");
        var timeStamp = FindChild<TextBlock>(border, "TimeStampTextBlock");

        if (closeButton != null && timeStamp != null && moreButton != null)
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
        var border = sender as Border;
        var pinIcon = FindChild<SymbolIcon>(border, "PinIcon");
        var closeButton = FindChild<Button>(border, "CloseButton");
        var moreButton = FindChild<Button>(border, "MoreButton");
        var timeStamp = FindChild<TextBlock>(border, "TimeStampTextBlock");

        // Check if MoreButton or its Flyout is open
        if (closeButton != null && timeStamp != null && moreButton != null)
        {
            // If the flyout is open, don't hide the buttons
            var flyout = moreButton.Flyout as MenuFlyout;
            if (flyout != null && flyout.IsOpen)
            {
                // Flyout is open, so don't change the visibility
                return;
            }

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
        var flyout = sender as MenuFlyout;
        if (flyout != null)
        {
            var moreButton = flyout.Target as Button;
            if (moreButton != null)
            {
                var parent = VisualTreeHelper.GetParent(moreButton) as FrameworkElement;
                var pinIcon = FindChild<SymbolIcon>(parent, "PinIcon");
                var closeButton = FindChild<Button>(parent, "CloseButton");
                var timeStamp = FindChild<TextBlock>(parent, "TimeStampTextBlock");

                if (closeButton != null)
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
        }
    }

    private async void OnNotificationFilterClick(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuFlyoutItem;
        if (menuItem != null)
        {
            string? appPackage = menuItem.Tag as string;

            if (!string.IsNullOrEmpty(appPackage))
            {
                ViewModel.UpdateNotificationFilter(appPackage);
            }
        }
    }

    private async void OnNotificationPinClick(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuFlyoutItem;
        if (menuItem != null)
        {
            string? notificationKey = menuItem.Tag as string;
            if (!string.IsNullOrEmpty(notificationKey))
            {
                ViewModel.PinNotification(notificationKey);
            }
        }
    }

    private async void OpenAppClick(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuFlyoutItem;
        if (menuItem != null)
        {
            if (menuItem.Tag is Data.Models.Notification notification)
            {
                //await ViewModel.OpenApp(notification);
            }
        }
    }

    private void OnNotificationCloseButtonClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.Tag is string notification)
        {
            ViewModel.RemoveNotification(notification);
        }
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        Button? button = sender as Button;
        if (button != null && button?.Tag is NotificationAction action)
        {

            ViewModel.HandleNotificationAction(action);
        }
    }


    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.Tag is Notification message)
        {
            var replyTextBox = FindChild<TextBox>(button.Parent, "ReplyTextBox");
            if (replyTextBox != null)
            {
                string replyText = replyTextBox.Text;
                // Clear the textbox after getting the text
                replyTextBox.Text = string.Empty;

                ViewModel.HandleNotificationReply(message, replyText);
            }
        }
    }

    private void ReplyTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        TextBox? textBox = sender as TextBox;

        if (textBox != null && e.Key == VirtualKey.Enter && textBox?.Tag is Notification message)
        {
            ViewModel.HandleNotificationReply(message, textBox.Text);
            textBox.Text = string.Empty;
        }
    }

    // Helper method to find a child element by name
    private T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        if (parent == null) return default;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T && (child as FrameworkElement).Name == childName)
            {
                return (T)child;
            }

            var childOfChild = FindChild<T>(child, childName);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }

        return default;
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
        if (show)
        {
            overlay.Visibility = Visibility.Visible;
        }

        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = show ? 0 : 1,
            To = show ? 1 : 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200))
        };

        Storyboard.SetTarget(animation, overlay);
        Storyboard.SetTargetProperty(animation, "Opacity");

        if (!show)
        {
            animation.Completed += (s, args) => overlay.Visibility = Visibility.Collapsed;
        }

        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private async void ToggleScreenMirror(object sender, TappedRoutedEventArgs e)
    {
        await ViewModel.StartScrcpy();
    }

    private void UpdateButtonClick(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.UpdateApp();
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        // Check if the dropped data contains files
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            // Get the file(s) from the DataPackage
            var items = await e.DataView.GetStorageItemsAsync();
            var files = items.OfType<StorageFile>().ToArray();
            ViewModel.SendFiles(files);
        }
    }

    private void Page_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        Debug.WriteLine("Drag Enter");
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop files to send";
    }
}
