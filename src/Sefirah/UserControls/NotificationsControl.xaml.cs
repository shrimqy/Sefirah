using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.ViewModels;
using Windows.System;

namespace Sefirah.UserControls;

public sealed partial class NotificationsControl : UserControl
{
    public MainPageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<MainPageViewModel>();

    public NotificationsControl()
    {
        InitializeComponent();
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

            if (pinIcon.Tag is bool isPinned && isPinned)
                pinIcon.Visibility = Visibility.Collapsed;

            pinIcon.IsHitTestVisible = true;
            closeButton.Opacity = 1;
            moreButton.Opacity = 1;
            border.Shadow = new ThemeShadow();
            border.Translation = new System.Numerics.Vector3(0, 0, 12);
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border &&
            border.FindName("PinIcon") is SymbolIcon pinIcon &&
            border.FindName("CloseButton") is Button closeButton &&
            border.FindName("MoreButton") is Button moreButton &&
            border.FindName("TimeStampTextBlock") is TextBlock timeStamp)
        {
            if (moreButton.Flyout is MenuFlyout flyout && flyout.IsOpen)
                return;

            timeStamp.Visibility = Visibility.Visible;

            if (pinIcon.Tag is bool isPinned && isPinned)
                pinIcon.Visibility = Visibility.Visible;

            pinIcon.IsHitTestVisible = false;
            closeButton.Opacity = 0;
            moreButton.Opacity = 0;
            border.Shadow = null;
            border.Translation = new System.Numerics.Vector3(0, 0, 0);
        }
    }

    private void MoreButtonFlyoutClosed(object sender, object e)
    {
        if (sender is MenuFlyout flyout && flyout.Target is Button moreButton &&
            VisualTreeHelper.GetParent(moreButton) is FrameworkElement parent &&
            parent.FindName("PinIcon") is SymbolIcon pinIcon &&
            parent.FindName("CloseButton") is Button closeButton &&
            parent.FindName("TimeStampTextBlock") is TextBlock timeStamp)
        {
            if (pinIcon.Tag is bool isPinned && isPinned)
                pinIcon.Visibility = Visibility.Visible;

            closeButton.Opacity = 0;
            moreButton.Opacity = 0;
            timeStamp.Visibility = Visibility.Visible;
        }
    }

    private async void OpenAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Notification notification)
            await ViewModel.OpenApp(notification);
    }

    private void UpdateNotificationFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string appPackage)
            ViewModel.UpdateNotificationFilter(appPackage);
    }

    private void ToggleNotificationPinClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Notification notification)
            ViewModel.ToggleNotificationPin(notification);
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is Notification notification &&
            (button.Parent as FrameworkElement)?.FindName("ReplyTextBox") is TextBox replyTextBox)
        {
            ViewModel.HandleNotificationReply(notification, replyTextBox.Text);
            replyTextBox.Text = string.Empty;
        }
    }

    private void ReplyTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is TextBox textBox &&
            e.Key is VirtualKey.Enter &&
            textBox.Tag is Notification message)
        {
            ViewModel.HandleNotificationReply(message, textBox.Text);
            textBox.Text = string.Empty;
        }
    }
}
