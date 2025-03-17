using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using Sefirah.App.ViewModels;

namespace Sefirah.App.Views;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; }
    public MainPage()
    {
        this.InitializeComponent();

        ViewModel = Ioc.Default.GetRequiredService<MainPageViewModel>();
        // Window customization
        Window window = MainWindow.Instance;
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(AppTitleBar);
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
            string? appName = menuItem.Tag as string; 

            if (!string.IsNullOrEmpty(appName))
            {
                await ViewModel.UpdateNotificationFilterAsync(appName, NotificationFilter.Disabled);
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
                await ViewModel.PinNotificationAsync(notificationKey);
            }
        }
    }

    private async void OpenAppClick(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuFlyoutItem;
        if (menuItem != null)
        {
            if (menuItem.Tag is Notification notification)
            {
                await ViewModel.OpenApp(notification);
            }
        }
    }

    private void OnNotificationCloseButtonClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        Debug.WriteLine("Close Button Clicked");
        if (button?.Tag is string notification)
        {
            ViewModel.RemoveNotification(notification);
        }
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        Debug.WriteLine("Action Button Clicked");
        if (button != null && button?.Tag is NotificationAction action)
        {

            var notificationAction = new NotificationAction
            {
                Label = action.Label,
                NotificationKey = action.NotificationKey,
                ActionIndex = action.ActionIndex,
                IsReplyAction = action.IsReplyAction
            };
            //SocketService.Instance.SendMessage(SocketMessageSerializer.Serialize(notificationAction));
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
                
                ViewModel.NotificationReplyCommand.Execute((message, replyText));
            }
        }
    }

    private void ReplyTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var textBox = sender as TextBox;

        if (textBox != null && e.Key == Windows.System.VirtualKey.Enter && textBox?.Tag is Notification message)
        {
            ViewModel.NotificationReplyCommand.Execute((message, textBox.Text));
            textBox.Text = string.Empty;
        }
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var selectedItem = (NavigationViewItem)args.SelectedItem;
        switch (selectedItem.Tag.ToString())
        {
            case "Settings":
                ContentFrame.Navigate(typeof(SettingsPage));
                break;
            case "Messages":
                ContentFrame.Navigate(typeof(MessagesPage));
                break;
            case "Apps":
                ContentFrame.Navigate(typeof(AppsPage));
                break;
        }
    }

    // Helper method to find a child element by name
    private T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        if (parent == null) return null;

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

        return null;
    }

    private T FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;

        T parent = parentObject as T;
        return parent ?? FindParent<T>(parentObject);
    }
}
