using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sefirah.App.Data.Models;
using Sefirah.App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Sefirah.App.Views;
public sealed partial class MessagesPage : Page
{
    public MessagesViewModel ViewModel { get; }
    private bool _pendingScrollToBottom = false;
    
    public MessagesPage()
    {
        this.InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<MessagesViewModel>();
        
        // Configure ScrollViewer for anchoring behavior
        MessagesScrollViewer.VerticalAnchorRatio = 1.0;
        
        // Watch for collection changes but only after the view is loaded
        this.Loaded += (s, e) => {
            // Set initial scroll position when conversation is selected
            ViewModel.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(ViewModel.SelectedConversation))
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => {
                        ScrollToBottom();
                    });
                }
            };
           
        };
        
        MessagesScrollViewer.ViewChanged += MessagesScrollViewer_ViewChanged;
    }
    
    private void MessagesScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        // Only update scroll position state when user interaction is complete
        if (!e.IsIntermediate)
        {
            if (_pendingScrollToBottom && !e.IsIntermediate)
            {
                _pendingScrollToBottom = false;
                ScrollToBottom();
            }
        }
    }
    
    private void ScrollToBottom()
    {
        if (MessagesScrollViewer == null)
            return;
        
        try
        {
            MessagesScrollViewer.ChangeView(null, MessagesScrollViewer.ScrollableHeight, null, true);
        }
        catch
        {
            // ignore
        }
    }
    
    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }
    
    private void MessageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            // If SHIFT is pressed, this next IF is skipped over, so the
            //     default behavior of "AcceptsReturn" is used.
            if (!InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
            {
                SendMessage();
                // Mark the event as handled, so the default behavior of 
                //    "AcceptsReturn" is not used.
                e.Handled = true;
            }
        }
    }
    
    private void SendMessage()
    {
        // Only send if there's actual text
        if (!string.IsNullOrWhiteSpace(ViewModel.MessageText))
        {
            ViewModel.SendMessage();
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => {
                ScrollToBottom();
            });
        }
    }
    
    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Only update suggestions when the user types something
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string searchText = sender.Text.Trim();
            
            if (string.IsNullOrEmpty(searchText))
            {
                sender.ItemsSource = null;
                return;
            }
            
            // Get matching conversations
            var suggestions = ViewModel.Conversations.Where(c => 
                // Check if address contains search text
                (c.Messages.Count > 0 && 
                 c.Messages[0].Addresses.Any(a => 
                    a.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase))) ||
                // Or if snippet contains search text
                c.Snippet.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            sender.ItemsSource = suggestions;
        }
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SmsConversation selectedConversation)
        {
            // Select the conversation in the ListView
            ViewModel.SelectedConversation = selectedConversation;
            
            // Clear the search box
            sender.Text = string.Empty;
            sender.ItemsSource = null;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // If they selected an item from the suggestions, use that
        if (args.ChosenSuggestion is SmsConversation selectedConversation)
        {
            ViewModel.SelectedConversation = selectedConversation;
        }
        // Otherwise search for the text they entered
        else if (!string.IsNullOrEmpty(args.QueryText))
        {
            // Get the best match
            var bestMatch = ViewModel.Conversations.Where(c => 
                // Check if address contains search text
                (c.Messages.Count > 0 && 
                 c.Messages[0].Addresses.Any(a => 
                    a.Address.Contains(args.QueryText, StringComparison.OrdinalIgnoreCase))) ||
                // Or if snippet contains search text
                c.Snippet.Contains(args.QueryText, StringComparison.OrdinalIgnoreCase)
            ).FirstOrDefault();
            
            if (bestMatch != null)
            {
                ViewModel.SelectedConversation = bestMatch;
            }
            
            // Clear the search box
            sender.Text = string.Empty;
            sender.ItemsSource = null;
        }
    }

    private async void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string messageText)
        {
            // Copy the message text to clipboard
            var dataPackage = new DataPackage();
            dataPackage.SetText(messageText);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void NewMessageButton_Click(object sender, RoutedEventArgs e)
    {
        // Start a new conversation
        ViewModel.StartNewConversation();
        
        // Focus the address input
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => {
            NewAddressTextBox?.Focus(FocusState.Programmatic);
        });
    }

    private void NewAddressTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            AddCurrentAddress();
        }
    }

    private void AddCurrentAddress()
    {
        if (string.IsNullOrWhiteSpace(ViewModel.NewConversationAddress))
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Error",
                Content = "Please enter a valid address.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            errorDialog.ShowAsync();
            
            NewAddressTextBox.Focus(FocusState.Programmatic);
            return;
        }
        
        ViewModel.AddAddressToNewConversation(ViewModel.NewConversationAddress);
        
        // Keep focus on the address input 
        NewAddressTextBox.Focus(FocusState.Programmatic);
    }

    private void RemoveAddressButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SmsAddress address)
        {
            ViewModel.RemoveAddressFromNewConversation(address);
        }
    }
}
