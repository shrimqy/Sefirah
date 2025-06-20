using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace Sefirah.Views;

public sealed partial class MessagesPage : Page
{
    public MessagesViewModel ViewModel { get; }

    public MessagesPage()
    {
        this.InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<MessagesViewModel>();
        DataContext = ViewModel;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(MessageTextBox.Text))
        {
            await ViewModel.SendMessageCommand.ExecuteAsync(MessageTextBox.Text);
            MessageTextBox.Text = string.Empty;
        }
    }

    private async void MessageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var shiftPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            
            if (!shiftPressed && !string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                e.Handled = true;
                await ViewModel.SendMessageCommand.ExecuteAsync(MessageTextBox.Text);
                MessageTextBox.Text = string.Empty;
            }
        }
    }

    private void NewMessageButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartNewConversationCommand.Execute(null);
    }

    private void NewAddressTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(NewAddressTextBox.Text))
        {
            ViewModel.AddAddressCommand.Execute(NewAddressTextBox.Text.Trim());
            NewAddressTextBox.Text = string.Empty;
        }
    }

    private void RemoveAddressButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string address)
        {
            ViewModel.RemoveAddressCommand.Execute(address);
        }
    }

    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string messageText)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(messageText);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.SearchConversationsCommand.Execute(sender.Text);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // Handle search submission if needed
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SmsConversation conversation)
        {
            ViewModel.SelectedConversation = conversation;
        }
    }
} 
