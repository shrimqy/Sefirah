using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;

namespace Sefirah.App.ViewModels;
public sealed class MessagesViewModel : BaseViewModel
{
    private ISmsHandlerService SmsHandlerService { get; } = Ioc.Default.GetRequiredService<ISmsHandlerService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();

    public ObservableCollection<SmsConversation> Conversations => SmsHandlerService.Conversations;

    private SmsConversation? _selectedConversation;
    public SmsConversation? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            // If selecting a conversation, exit new conversation mode
            if (value != null)
            {
                IsNewConversation = false;
            }
            
            if (SetProperty(ref _selectedConversation, value))
            {
                // When the selected conversation changes, request thread history
                if (value != null)
                {
                    Debug.WriteLine($"Requesting thread history for ThreadId: {value.ThreadId}");
                    SmsHandlerService.RequestThreadHistory(value.ThreadId);
                    OnPropertyChanged(nameof(IsExistingConversationSelected));
                    OnPropertyChanged(nameof(ShouldShowComposeUI));
                    OnPropertyChanged(nameof(ShouldShowEmptyState));
                    OnPropertyChanged(nameof(ConversationMessages));
                }
            }
        }
    }

    public ObservableCollection<PhoneNumber> PhoneNumbers { get; set; } = [];

    public ObservableCollection<TextMessage>? ConversationMessages => SelectedConversation?.Messages;

    private string _messageText = string.Empty;
    public string MessageText
    {
        get => _messageText;
        set => SetProperty(ref _messageText, value);
    }
    
    private bool _isNewConversation;
    public bool IsNewConversation
    {
        get => _isNewConversation;
        set 
        { 
            if (SetProperty(ref _isNewConversation, value))
            {
                // Notify that helper properties have changed
                OnPropertyChanged(nameof(IsExistingConversationSelected));
                OnPropertyChanged(nameof(ShouldShowComposeUI));
                OnPropertyChanged(nameof(ShouldShowEmptyState));
            }
        }
    }
    
    private string _newConversationAddress = string.Empty;
    public string NewConversationAddress
    {
        get => _newConversationAddress;
        set => SetProperty(ref _newConversationAddress, value);
    }
    
    // List of addresses for the new conversation
    private ObservableCollection<SmsAddress> _newConversationAddresses = [];
    public ObservableCollection<SmsAddress> NewConversationAddresses
    {
        get => _newConversationAddresses;
        private set => SetProperty(ref _newConversationAddresses, value);
    }
    
    // Selected SIM card (subscription ID)
    private int _selectedSubscriptionId;
    public int SelectedSubscriptionId 
    {
        get => _selectedSubscriptionId;
        set => SetProperty(ref _selectedSubscriptionId, value);
    }

    // Helper properties for UI visibility
    public bool IsExistingConversationSelected => SelectedConversation != null && !IsNewConversation;

    public bool ShouldShowComposeUI => IsNewConversation || SelectedConversation != null;

    public bool ShouldShowEmptyState => SelectedConversation == null && !IsNewConversation;

    public MessagesViewModel()
    {
        LoadPhoneNumbers();

        SmsHandlerService.Conversations.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(Conversations));
            
            if (e.NewItems != null)
            {
                // If we have no selected conversation, select the newest one
                if (SelectedConversation == null && e.NewItems.Count > 0 && !IsNewConversation)
                {
                    SelectedConversation = e.NewItems[0] as SmsConversation;
                }
            }
        };
    }

    private async void LoadPhoneNumbers()
    {
        PhoneNumbers = await DeviceManager.GetLastConnectedDevicePhoneNumbersAsync();
        logger.Info($"Loaded {PhoneNumbers.Count} phone numbers");
        if (PhoneNumbers != null) 
        {
            SelectedSubscriptionId = PhoneNumbers[0].SubscriptionId;
        }
        OnPropertyChanged(nameof(PhoneNumbers));
    }

    public void StartNewConversation()
    {
        // Clear any selected conversation
        SelectedConversation = null;
        
        // Set new conversation mode
        IsNewConversation = true;
        NewConversationAddress = string.Empty;
        NewConversationAddresses.Clear();
        
        // Set default subscription ID
        SelectedSubscriptionId = PhoneNumbers[0].SubscriptionId;
        
        MessageText = string.Empty;
    }
    
    public void AddAddressToNewConversation(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return;
            
        // Check if address already exists in the list
        if (!NewConversationAddresses.Any(a => a.Address.Equals(address, StringComparison.OrdinalIgnoreCase)))
        {
            NewConversationAddresses.Add(new SmsAddress { Address = address });
        }
        
        // Clear the address input field
        NewConversationAddress = string.Empty;
    }
    
    public void RemoveAddressFromNewConversation(SmsAddress address)
    {
        NewConversationAddresses.Remove(address);
    }

    public async void SendMessage()
    {
        // Check if this is for a new conversation
        if (IsNewConversation)
        {
            // Check if we have any addresses and a message
            if (NewConversationAddresses.Count == 0 || string.IsNullOrWhiteSpace(MessageText))
                return;

            var subscriptionId = SelectedSubscriptionId;
            if (subscriptionId == -1)
            {
                subscriptionId = PhoneNumbers.FirstOrDefault(p => p.Number == NewConversationAddresses[0].Address)?.SubscriptionId ?? -1;
            }

            var newMessage = new TextMessage
            {
                Body = MessageText,
                Addresses = [.. NewConversationAddresses],
                SubscriptionId = SelectedSubscriptionId,
                MessageType = 2, 
                ThreadId = null  
            };
            
            // Send the message
            await SmsHandlerService.SendTextMessage(newMessage);            
            
            // Reset
            IsNewConversation = false;
            SelectedConversation = null;
        }
        else if (SelectedConversation != null && !string.IsNullOrWhiteSpace(MessageText))
        {
            // Normal case - sending to existing conversation
            List<SmsAddress>? recipientAddress = SelectedConversation.Messages.FirstOrDefault()?.Addresses;
            if (recipientAddress == null)
            {
                return;
            }

            var textMessage = new TextMessage 
            {
                Body = MessageText, 
                Addresses = recipientAddress,
                SubscriptionId = SelectedConversation.Messages.Last().SubscriptionId,
                MessageType = 2  // Sent message
            };
                  
            await SmsHandlerService.SendTextMessage(textMessage);
        }
        
        MessageText = string.Empty;
    }
}
