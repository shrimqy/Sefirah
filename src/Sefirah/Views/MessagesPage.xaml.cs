using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.UserControls;
using Sefirah.Utils;
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
        Loaded += (_, _) => UpdatePaneLayout();
    }

    private ScrollViewer? messagesScrollViewer;

    private void MessagesListView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (messagesScrollViewer is not null)
            return;

        messagesScrollViewer = MessagesListView.FindDescendant<ScrollViewer>(x => x.Name == "ScrollViewer");
        messagesScrollViewer?.ViewChanged += MessagesScrollViewer_ViewChanged;
    }

    private async void MessagesScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
#if WINDOWS
        if (MessagesListView.ItemsPanelRoot is ItemsStackPanel { FirstVisibleIndex: 0 })
#else
        // ItemsStackPanel.FirstVisibleIndex is unimplemented on Uno Skia/WASM:
        // https://platform.uno/docs/articles/implemented/microsoft-ui-xaml-controls-itemsstackpanel.html
        if (sender is ScrollViewer { VerticalOffset: < 200 })
#endif
            await ViewModel.LoadOlderMessages();
    }

    private const double TwoPaneMinWidth = 720;
    private const double WideThreadsWidth = 420;

    private bool _showDetail;
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

    private async void SendMessage()
    {
        if (!string.IsNullOrWhiteSpace(MessageTextBox.Text) || ViewModel.StagedAttachments.Count > 0)
        {
            await ViewModel.SendMessage(MessageTextBox.Text);
            MessageTextBox.Text = string.Empty;
        }
    }

    private async void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        var files = await PickerHelper.PickMultipleFilesAsync(
        [
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
            ".mp4", ".3gp",
            ".mp3", ".m4a", ".aac", ".ogg"
        ]);
        if (files.Count > 0)
            await ViewModel.AddFiles(files);
    }

    private void Composer_DragOver(object sender, DragEventArgs e)
    {
        if (!ViewModel.ShouldShowComposeUI || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "MessageAttachDropCaption".GetLocalizedResource();
        e.Handled = true;
    }

    private async void Composer_Drop(object sender, DragEventArgs e)
    {
        if (!ViewModel.ShouldShowComposeUI || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        e.Handled = true;
        var items = await e.DataView.GetStorageItemsAsync();
        var files = items.OfType<StorageFile>().ToList();
        if (files.Count > 0)
            await ViewModel.AddFiles(files);
    }

    private void RemoveStagedAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is StagedAttachment attachment)
        {
            ViewModel.RemoveStagedAttachment(attachment);
        }
    }

    private async void PrefetchGif_Handler(object sender, RoutedEventArgs e)
    {
        if (sender is MessageAttachmentView { Attachment: { } attachment })
            await ViewModel.PrefetchGifAsync(attachment);
    }

    private async void OpenAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MessageAttachmentView { Attachment: { } attachment })
            await ViewModel.OpenAttachment(attachment);
    }

    private async void OpenAttachmentWith_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MessageAttachmentView { Attachment: { } attachment })
            await ViewModel.OpenAttachmentWith(attachment);
    }

    private async void ShowAttachmentInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MessageAttachmentView { Attachment: { } attachment })
            await ViewModel.ShowAttachmentInFolder(attachment);
    }

    private void NewMessageButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartNewConversation();
        ShowConversationDetail();
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
        if (args.ChosenSuggestion is not Conversation conversation)
            return;

        ViewModel.SelectedConversation = conversation;
        sender.Text = string.Empty;
        sender.ItemsSource = null;
        ShowConversationDetail();
    }

    private void MessagesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        ViewModel.SelectedConversation = e.ClickedItem as Conversation;
        ShowConversationDetail();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdatePaneLayout();

    private void ConversationBackButton_Click(object sender, RoutedEventArgs e)
    {
        _showDetail = false;
        UpdatePaneLayout();
    }

    private void ShowConversationDetail()
    {
        _showDetail = true;
        UpdatePaneLayout();
    }

    private void UpdatePaneLayout()
    {
        var narrow = ActualWidth > 0 && ActualWidth < TwoPaneMinWidth;
        ConversationBackButton.Visibility = narrow && _showDetail ? Visibility.Visible : Visibility.Collapsed;

        if (!narrow)
        {
            ThreadsColumn.Width = new GridLength(WideThreadsWidth);
            DetailColumn.Width = new GridLength(1, GridUnitType.Star);
            ThreadsPane.Visibility = Visibility.Visible;
            DetailPane.Visibility = Visibility.Visible;
            return;
        }

        ThreadsPane.Visibility = _showDetail ? Visibility.Collapsed : Visibility.Visible;
        DetailPane.Visibility = _showDetail ? Visibility.Visible : Visibility.Collapsed;
        ThreadsColumn.Width = _showDetail ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        DetailColumn.Width = _showDetail ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
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
        if (args.ChosenSuggestion is Contact contact)
            ViewModel.AddAddress(contact);
        else if (!string.IsNullOrWhiteSpace(args.QueryText))
            ViewModel.AddAddress(new Contact(args.QueryText));

        sender.Text = string.Empty;
        sender.ItemsSource = null;
    }
}
