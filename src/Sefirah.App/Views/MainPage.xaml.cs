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
        var closeButton = FindChild<Button>(border, "CloseButton");
        var moreButton = FindChild<Button>(border, "MoreButton");
        var timeStamp = FindChild<TextBlock>(border, "TimeStampTextBlock");
        if (closeButton != null && timeStamp != null && moreButton != null)
        {
            timeStamp.Visibility = Visibility.Collapsed;
            closeButton.Visibility = Visibility.Visible;
            closeButton.Opacity = 1;
            closeButton.IsHitTestVisible = true;
            moreButton.Opacity = 1;
            moreButton.IsHitTestVisible = true;
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        var border = sender as Border;
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
                // Flyout is open, so don't change the opacity or hit testing
                return;
            }

            // Reset the buttons when the flyout is not open
            timeStamp.Visibility = Visibility.Visible;
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
            // Now get the MoreButton that owns the flyout
            var moreButton = flyout.Target as Button;
            if (moreButton != null)
            {
                // Find the CloseButton within the same parent (e.g., the same StackPanel or Border)
                var parent = VisualTreeHelper.GetParent(moreButton) as FrameworkElement;
                var closeButton = FindChild<Button>(parent, "CloseButton");
                var timeStamp = FindChild<TextBlock>(parent, "TimeStampTextBlock");

                if (closeButton != null)
                {
                    // Reset the opacity and hit testing after the flyout is closed
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
        // Retrieve the AppPackage from the Flyout's Tag or DataContext (depending on your binding)
        var menuItem = sender as MenuFlyoutItem;
        if (menuItem != null)
        {
            string? appName = menuItem.Tag as string;  // Assume AppPackage is set as Tag or DataContext

            if (!string.IsNullOrEmpty(appName))
            {
                // Update the database to set NotificationFilter to DISABLED
                await ViewModel.UpdateNotificationFilterAsync(appName, NotificationFilter.Disabled);

                // TODO reflect the change in the UI
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


    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button != null)
        {
            // Find the parent StackPanel
            var stackPanel = FindParent<StackPanel>(button);
            if (stackPanel != null)
            {
                // Get the ReplyTextBox in the same StackPanel
                var replyTextBox = stackPanel.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "ReplyTextBox");
                string replyText = replyTextBox?.Text;
                // Clear the textbox after sending
                replyTextBox.Text = string.Empty;

                // Retrieve NotificationKey from the Tag of the SendButton
                string notificationKey = button.Tag as string;

                //HandleReply(notificationKey, replyText);

            }
        }
    }

    private void ReplyTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var textBox = sender as TextBox;

        if (textBox != null && e.Key == Windows.System.VirtualKey.Enter && textBox?.Tag is NotificationMessage message)
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
