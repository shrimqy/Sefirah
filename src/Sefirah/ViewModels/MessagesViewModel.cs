using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Services;

namespace Sefirah.ViewModels;
public sealed partial class MessagesViewModel : BaseViewModel
{
    private readonly SmsHandlerService smsHandlerService;
    private readonly IDeviceManager deviceManager;
    private readonly ILogger<MessagesViewModel> logger;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher;

    // Active conversations for UI binding (changes based on active device)
    public ObservableCollection<SmsConversation>? Conversations { get; private set; }

    // Search functionality
    public ObservableCollection<SmsConversation> SearchResults { get; } = [];

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
                LoadMessagesForSelectedConversation();
                OnPropertyChanged(nameof(IsExistingConversationSelected));
                OnPropertyChanged(nameof(ShouldShowComposeUI));
                OnPropertyChanged(nameof(ShouldShowEmptyState));
                OnPropertyChanged(nameof(Messages));
            }
        }
    }

    public ObservableCollection<TextMessage> Messages { get; } = [];

    [ObservableProperty]
    public partial bool IsNewConversation { get; set; }

    [ObservableProperty]
    public partial string NewConversationAddress { get; set; } = string.Empty;

    public ObservableCollection<string> NewConversationAddresses { get; } = [];

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    // Phone numbers from active device
    public ObservableCollection<PhoneNumber> PhoneNumbers { get; } = [];

    [ObservableProperty]
    public partial int SelectedSubscriptionId { get; set; }

    // Current device reference
    public PairedDevice? ActiveDevice => deviceManager.ActiveDevice;

    // UI State Properties
    public bool ShouldShowEmptyState => !IsNewConversation && SelectedConversation == null;
    public bool IsExistingConversationSelected => !IsNewConversation && SelectedConversation != null;
    public bool ShouldShowComposeUI => IsNewConversation || SelectedConversation != null;

    public MessagesViewModel()
    {
        smsHandlerService = Ioc.Default.GetRequiredService<SmsHandlerService>();
        deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
        logger = Ioc.Default.GetRequiredService<ILogger<MessagesViewModel>>();
        dispatcher = App.MainWindow!.DispatcherQueue;

        // Listen to device changes
        ((INotifyPropertyChanged)deviceManager).PropertyChanged += OnDeviceManagerPropertyChanged;

        // Listen to conversation updates from SMS handler
        smsHandlerService.ConversationsUpdated += OnConversationsUpdated;

        // Initialize with current active device
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (ActiveDevice != null)
        {
            await LoadConversationsForActiveDevice();
            LoadPhoneNumbers();
        }
    }

    private async void OnDeviceManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IDeviceManager.ActiveDevice))
        {
            OnPropertyChanged(nameof(ActiveDevice));
            await LoadConversationsForActiveDevice();
            LoadPhoneNumbers();
            
            // Clear selected conversation when device changes
            SelectedConversation = null;
            IsNewConversation = false;
        }
    }

    private void LoadPhoneNumbers()
    {
        PhoneNumbers.Clear();
        if (ActiveDevice?.PhoneNumbers != null)
        {
            foreach (var phoneNumber in ActiveDevice.PhoneNumbers)
            {
                PhoneNumbers.Add(phoneNumber);
            }
            
            // Set default subscription
            if (PhoneNumbers.Count > 0)
            {
                SelectedSubscriptionId = PhoneNumbers[0].SubscriptionId;
            }
        }
    }

    private async Task LoadConversationsForActiveDevice()
    {
        if (ActiveDevice == null)
        {
            Conversations = null;
            OnPropertyChanged(nameof(Conversations));
            return;
        }

        try
        {
            // Load conversations from database first
            await smsHandlerService.LoadConversationsFromDatabase(ActiveDevice.Id);

            // Get the device-specific conversations reference (same ObservableCollection)
            Conversations = smsHandlerService.GetConversationsForDevice(ActiveDevice.Id);
            OnPropertyChanged(nameof(Conversations));

            logger.LogInformation("Loaded {Count} conversations for device: {DeviceId}", 
                Conversations.Count, ActiveDevice.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading conversations for device: {DeviceId}", ActiveDevice?.Id);
        }
    }

    private async void LoadMessagesForSelectedConversation()
    {
        if (SelectedConversation == null || ActiveDevice == null) return;

        try
        {
            // Clear previous conversation messages
            Messages.Clear();

            // Load messages from conversation object first
            if (SelectedConversation.Messages.Count > 0)
            {
                foreach (var message in SelectedConversation.Messages.OrderBy(m => m.Timestamp))
                {
                    Messages.Add(message);
                }
            }

            // Load additional messages from database
            var dbMessages = await smsHandlerService.LoadMessagesForConversation(ActiveDevice.Id, SelectedConversation.ThreadId);
            if (dbMessages.Count > 0)
            {
                var existingIds = new HashSet<long>(Messages.Select(m => m.UniqueId));
                var newMessages = dbMessages.Where(m => !existingIds.Contains(m.UniqueId)).OrderBy(m => m.Timestamp);
                
                foreach (var message in newMessages)
                {
                    Messages.Add(message);
                }
            }

            // Request thread history from device
            if (ActiveDevice.Session != null)
            {
                await smsHandlerService.RequestThreadHistory(ActiveDevice.Session, SelectedConversation.ThreadId);
            }

            logger.LogInformation("Loaded {Count} messages for conversation: {ThreadId}", 
                Messages.Count, SelectedConversation.ThreadId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading messages for conversation: {ThreadId}", SelectedConversation.ThreadId);
        }
    }

    [RelayCommand]
    public async Task SendMessage(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText) || ActiveDevice?.Session == null)
        {
            return;
        }

        try
        {
            List<string> recipients = [];

            if (IsNewConversation)
            {
                recipients = NewConversationAddresses.ToList();
                if (recipients.Count == 0) return;
            }
            else if (SelectedConversation != null)
            {
                var recipientAddress = SelectedConversation.Messages
                    .SelectMany(m => m.Addresses)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(recipientAddress)) return;
                recipients.Add(recipientAddress);
            }
            else
            {
                return;
            }

            var textMessage = new TextMessage
            {
                ThreadId = SelectedConversation?.ThreadId,
                Body = messageText.Trim(),
                Addresses = recipients,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UniqueId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MessageType = 2, // SENT
                Read = true,
                SubscriptionId = SelectedSubscriptionId
            };

            await smsHandlerService.SendTextMessage(ActiveDevice.Session, textMessage);
            
            MessageText = string.Empty;
            
            if (IsNewConversation)
            {
                IsNewConversation = false;
                NewConversationAddresses.Clear();
                NewConversationAddress = string.Empty;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending message");
        }
    }

    [RelayCommand]
    public void StartNewConversation()
    {
        IsNewConversation = true;
        SelectedConversation = null;
        NewConversationAddresses.Clear();
        NewConversationAddress = string.Empty;
        MessageText = string.Empty;
    }

    [RelayCommand]
    public void AddAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return;

        var trimmedAddress = address.Trim();
        if (NewConversationAddresses.Any(a => a.Equals(trimmedAddress, StringComparison.OrdinalIgnoreCase)))
            return;

        NewConversationAddresses.Add(trimmedAddress);
        NewConversationAddress = string.Empty;
    }

    [RelayCommand]
    public void RemoveAddress(string address)
    {
        NewConversationAddresses.Remove(address);
    }

    [RelayCommand]
    public async Task RefreshConversations()
    {
        await LoadConversationsForActiveDevice();
    }

    [RelayCommand]
    public void SearchConversations(string searchText)
    {
        SearchResults.Clear();
        
        if (string.IsNullOrWhiteSpace(searchText) || Conversations == null)
            return;

        var filtered = Conversations
            .Where(c => c.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       c.LastMessage.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var conversation in filtered)
        {
            SearchResults.Add(conversation);
        }
    }

    private void OnConversationsUpdated(object? sender, string deviceId)
    {
        if (ActiveDevice?.Id == deviceId)
        {
            dispatcher.EnqueueAsync(() =>
            {
                // Don't change the reference, just notify that conversations updated
                OnPropertyChanged(nameof(Conversations));
                
                // If we have a selected conversation and new messages arrived, add them
                if (SelectedConversation != null)
                {
                    var updatedConversation = Conversations?.FirstOrDefault(c => c.ThreadId == SelectedConversation.ThreadId);
                    if (updatedConversation != null)
                    {
                        var existingIds = new HashSet<long>(Messages.Select(m => m.UniqueId));
                        var newMessages = updatedConversation.Messages
                            .Where(m => !existingIds.Contains(m.UniqueId))
                            .OrderBy(m => m.Timestamp);
                        
                        foreach (var message in newMessages)
                        {
                            Messages.Add(message);
                        }
                    }
                }
            });
        }
    }
}
