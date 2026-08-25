using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.ViewModels;
using Windows.ApplicationModel.DataTransfer;
#if WINDOWS
using Windows.UI.ViewManagement.Core;
#endif

namespace Sefirah.Views;

public sealed partial class MessagesPage : Page
{
    public MessagesViewModel ViewModel { get; }

    public MessagesPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<MessagesViewModel>();
        DataContext = ViewModel;
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

#if WINDOWS
    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        MessageTextBox.Focus(FocusState.Programmatic);
        // Defer so the TextBox has focus before the emoji panel opens.
        DispatcherQueue.TryEnqueue(() =>
            CoreInputView.GetForCurrentView().TryShow(CoreInputViewKind.Emoji));
    }
#endif
    private void MessageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not Windows.System.VirtualKey.Enter)
            return;

        e.Handled = true;

        var shiftPressed = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        // Workaround: Uno Skia's TextBox inserts Enter via OnPostKeyDown/OnKeyDownSkia even when
        // PreviewKeyDown sets Handled=true (AcceptsReturn path ignores Handled). Keep AcceptsReturn
        // false and insert newlines ourselves on Shift+Enter so Enter-to-send works on Desktop/Linux.
        if (shiftPressed)
        {
            InsertNewLine();
            return;
        }

        SendMessage();
    }

    private void InsertNewLine()
    {
        var start = MessageTextBox.SelectionStart;
        var length = MessageTextBox.SelectionLength;
        var text = MessageTextBox.Text ?? string.Empty;

        MessageTextBox.Text = string.Concat(text.AsSpan(0, start), "\r", text.AsSpan(start + length));
        MessageTextBox.SelectionStart = start + 1;
    }

    private void SendMessage()
    {
        if (!string.IsNullOrWhiteSpace(MessageTextBox.Text))
        {
            ViewModel.SendMessage(MessageTextBox.Text);
            MessageTextBox.Text = string.Empty;
        }
    }

    private void NewMessageButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartNewConversation();
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
        if (args.Reason is AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = ViewModel.SearchConversations(sender.Text);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Conversation conversation)
        {
            ViewModel.SelectedConversation = conversation;
            sender.Text = string.Empty;
            sender.ItemsSource = null;
        }
    }

    private void MessagesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        ViewModel.SelectedConversation = e.ClickedItem as Conversation;
    }

    private void AddressInput_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Contact contact)
        {
            ViewModel.AddAddress(contact);
            sender.Text = string.Empty;
            sender.ItemsSource = null;
        }
    }

    private void RemoveAddressButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Contact address)
        {
            ViewModel.RemoveAddress(address);
        }
    }

    private void AddressInput_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason is AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = ViewModel.SearchContacts(sender.Text);
        }
    }

    private void AddressInput_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var address = args.QueryText;
        if (!string.IsNullOrWhiteSpace(address))
        {
            ViewModel.AddAddress(new Contact(address));
        }

        sender.Text = string.Empty;
        sender.ItemsSource = null;
    }
}
