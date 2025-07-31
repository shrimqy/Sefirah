using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models.Messages;
using Sefirah.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace Sefirah.Views;

public sealed partial class MessagesPage : Page
{
    public MessagesViewModel ViewModel { get; }

    private readonly Random _random = new();
    private readonly Dictionary<long, Windows.UI.Color> conversationColors = [];
    private readonly Dictionary<string, Windows.UI.Color> contactColors = [];

    public MessagesPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<MessagesViewModel>();
        DataContext = ViewModel;

        Loaded += (s, e) => {
            ViewModel.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(ViewModel.SelectedConversation))
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => {
                        UpdateAvatarColors();
                    });
                }
            };
        };
        
        ViewModel.PropertyChanged += (sender, args) => {
            if (args.PropertyName == nameof(ViewModel.Conversations))
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => {
                    UpdateConversationListColors();
                });
            }
        };
    }

    private void UpdateAvatarColors()
    {
        if (ViewModel.SelectedConversation == null || AvatarGlyphBorder == null || AvatarGlyphIcon == null)
            return;

        var conversationId = ViewModel.SelectedConversation.ThreadId;
        var backgroundColor = GetOrCreateConversationColor(conversationId);

        AvatarGlyphBorder.Background = new SolidColorBrush(backgroundColor);
    }

    private void UpdateConversationListColors()
    {
        if (ViewModel.Conversations == null) return;

        foreach (var conversation in ViewModel.Conversations)
        {
            var conversationId = conversation.ThreadId;
            GetOrCreateConversationColor(conversationId);
        }
    }

    private Windows.UI.Color GetOrCreateConversationColor(long conversationId)
    {
        if (!conversationColors.TryGetValue(conversationId, out var color))
        {
            color = GenerateRandomColor();
            conversationColors[conversationId] = color;
        }
        return color;
    }

    private Windows.UI.Color GetOrCreateContactColor(string address)
    {
        if (!contactColors.TryGetValue(address, out var color))
        {
            color = GenerateRandomColor();
            contactColors[address] = color;
        }
        return color;
    }

    private Windows.UI.Color GenerateRandomColor()
    {
        var hue = _random.Next(0, 360);
        var saturation = _random.Next(65, 85) / 100.0; // 65-85% saturation
        var lightness = _random.Next(45, 65) / 100.0; // 45-65% lightness 
        
        return HslToRgb(hue, saturation, lightness);
    }

    private static readonly char[] separator = [' ', '\t', '\n', '\r'];
    private static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "?";

        var words = displayName.Trim().Split(separator, StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length == 0)
            return "?";
        
        if (words.Length == 1)
        {
            // Single word - take first two characters or just first if only one character
            return words[0][..1].ToUpper();
        }
        
        // Multiple words - take first letter of first two words
        return (string.Concat(words[0].AsSpan()[..1], words[1].AsSpan(0, 1))).ToUpper();
    }

    private Windows.UI.Color HslToRgb(double h, double s, double l)
    {
        double r, g, b;

        if (s == 0)
        {
            r = g = b = l; // achromatic
        }
        else
        {
            double hue2rgb(double p, double q, double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0/6.0) return p + (q - p) * 6 * t;
                if (t < 1.0/2.0) return q;
                if (t < 2.0/3.0) return p + (q - p) * (2.0/3.0 - t) * 6;
                return p;
            }

            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            
            h /= 360;
            r = hue2rgb(p, q, h + 1.0/3.0);
            g = hue2rgb(p, q, h);
            b = hue2rgb(p, q, h - 1.0/3.0);
        }

        return Windows.UI.Color.FromArgb(255, 
            (byte)Math.Round(r * 255), 
            (byte)Math.Round(g * 255), 
            (byte)Math.Round(b * 255));
    }

    private void ConversationAvatarBorder_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is Border border)
        {
            if (border.DataContext is Conversation conversation)
            {
                var conversationId = conversation.ThreadId;
                var backgroundColor = GetOrCreateConversationColor(conversationId);
                border.Background = new SolidColorBrush(backgroundColor);
                
                // Set initials for text content if it's a TextBlock
                if (border.Child is TextBlock textBlock)
                {
                    textBlock.Text = MessagesPage.GetInitials(conversation.DisplayName);
                }
            }
            else if (border.DataContext is Contact contact)
            {
                // Handle contact suggestion avatars
                var backgroundColor = GetOrCreateContactColor(contact.Address);
                border.Background = new SolidColorBrush(backgroundColor);
                
                // Set initials for text content if it's a TextBlock
                if (border.Child is TextBlock textBlock && contact.DisplayName != null)
                {
                    textBlock.Text = GetInitials(contact.DisplayName);
                }
            }
        }
    }


    private void MessageAvatarBorder_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is Border border && border.DataContext is MessageGroup messageGroup)
        {
            // Use sender's address as the key so all messages from same sender have same color
            var backgroundColor = GetOrCreateContactColor(messageGroup.Sender.Address);
            border.Background = new SolidColorBrush(backgroundColor);
            
            // Set initials for text content if it's a TextBlock
            if (border.Child is TextBlock textBlock && messageGroup.Sender.DisplayName != null)
            {
                textBlock.Text = GetInitials(messageGroup.Sender.DisplayName);
            }
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(MessageTextBox.Text))
        {
            ViewModel.SendMessage(MessageTextBox.Text);
            MessageTextBox.Text = string.Empty;
        }
    }

    private void MessageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var shiftPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            
            if (!shiftPressed && !string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                e.Handled = true;
                ViewModel.SendMessage(MessageTextBox.Text);
                MessageTextBox.Text = string.Empty;
            }
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
        if (args.SelectedItem is Conversation conversation)
        {
            ViewModel.SelectedConversation = conversation;
        }
    }

    private void MessagesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        ViewModel.SelectedConversation = e.ClickedItem as Conversation;
    }

    private void AddressInput_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem != null && args.SelectedItem is Contact contact)
        {
            ViewModel.AddAddress(contact);
            sender.Text = string.Empty;
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
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.SearchContacts(sender.Text);
        }
    }

    private void AddressInput_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var address = args.QueryText;
        if (!string.IsNullOrWhiteSpace(address))
        {
            ViewModel.AddAddress(new Contact(address, address));
        }
        sender.Text = string.Empty;
    }
}
