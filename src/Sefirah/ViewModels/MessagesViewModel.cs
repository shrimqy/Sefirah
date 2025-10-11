using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Services;

namespace Sefirah.ViewModels;
public sealed partial class MessagesViewModel : BaseViewModel
{
    #region Services
    private readonly SmsHandlerService smsHandlerService = Ioc.Default.GetRequiredService<SmsHandlerService>();
    private readonly IDeviceManager deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    #endregion

    #region Properties
    public ObservableCollection<Conversation>? Conversations { get; private set; }
    public ObservableCollection<Conversation> SearchResults { get; } = [];
    public ObservableCollection<Contact> SearchContactsResults { get; } = [];
    private HashSet<long> MessageIds { get; set; } = [];

    public ObservableCollection<Contact> Contacts { get; set; } = [];

    private ObservableCollection<MessageGroup> messageGroups = [];
    public ObservableCollection<MessageGroup> MessageGroups
    {
        get => messageGroups;
        set => SetProperty(ref messageGroups, value);
    }

    private Conversation? selectedConversation;
    public Conversation? SelectedConversation
    {
        get => selectedConversation;
        set
        {
            // If selecting a conversation, exit new conversation mode 
            if (value != null)
            {
                IsNewConversation = false;
            }

            if (SetProperty(ref selectedConversation, value))
            {
                LoadMessagesForSelectedConversation();
                OnPropertyChanged(nameof(ShouldShowComposeUI));
                OnPropertyChanged(nameof(ShouldShowEmptyState));
            }
        }
    }

    [ObservableProperty]
    public partial bool IsNewConversation { get; set; }

    public ObservableCollection<Contact> NewConversationRecipients { get; } = [];

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    public ObservableCollection<PhoneNumber> PhoneNumbers { get; } = [];

    [ObservableProperty]
    public partial int SelectedSubscriptionId { get; set; }

    public PairedDevice? ActiveDevice => deviceManager.ActiveDevice;

    public bool ShouldShowEmptyState => !IsNewConversation && SelectedConversation == null;
    public bool ShouldShowComposeUI => IsNewConversation || SelectedConversation != null;
    #endregion

    public MessagesViewModel()
    {
        ((INotifyPropertyChanged)deviceManager).PropertyChanged += OnDeviceManagerPropertyChanged;
        smsHandlerService.ConversationsUpdated += OnConversationsUpdated;

        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        if (ActiveDevice is not null)
        {
            await LoadConversationsForActiveDevice();
            LoadPhoneNumbers();
        }
    }

    private void OnDeviceManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IDeviceManager.ActiveDevice))
        {
            OnPropertyChanged(nameof(ActiveDevice));
            InitializeAsync();

            SelectedConversation = null;
            IsNewConversation = false;
        }
    }

    private void LoadPhoneNumbers()
    {
        PhoneNumbers.Clear();
        if (ActiveDevice?.PhoneNumbers is not null)
        {
            foreach (var phoneNumber in ActiveDevice.PhoneNumbers)
            {
                PhoneNumbers.Add(phoneNumber);
            }
            
            if (PhoneNumbers.Count > 0)
            {
                SelectedSubscriptionId = PhoneNumbers[0].SubscriptionId;
            }
        }
    }

    private async void LoadContacts()
    {
        Contacts = await smsHandlerService.GetAllContactsAsync();
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
            await smsHandlerService.LoadConversationsFromDatabase(ActiveDevice.Id);

            Conversations = smsHandlerService.GetConversationsForDevice(ActiveDevice.Id);
            OnPropertyChanged(nameof(Conversations));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading conversations for device: {DeviceId}", ActiveDevice?.Id);
        }
    }

    private async void LoadMessagesForSelectedConversation()
    {
        if (SelectedConversation == null || ActiveDevice == null) return;

        try
        {
            var dbMessages = await smsHandlerService.LoadMessagesForConversation(ActiveDevice.Id, SelectedConversation.ThreadId);
            
            MessageGroups.Clear();
            
            if (dbMessages.Count > 0)
            {
                var sortedMessages = dbMessages.OrderBy(m => m.Timestamp).ToList();
                
                MessageIds.AddRange(sortedMessages.Select(m => m.UniqueId));
                
                BuildMessageGroups(sortedMessages);
            }

            // Request thread history from device
            if (ActiveDevice.Session != null)
            {
                smsHandlerService.RequestThreadHistory(ActiveDevice.Session, SelectedConversation.ThreadId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading messages for conversation: {ThreadId}", SelectedConversation.ThreadId);
        }
    }

    public void SendMessage(string messageText)
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
                recipients = NewConversationRecipients.Select(c => c.Address).ToList();
                if (recipients.Count == 0) return;
            }
            else if (SelectedConversation != null)
            {
                recipients = SelectedConversation.Contacts.Select(s => s.Address).ToList();
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

            smsHandlerService.SendTextMessage(ActiveDevice.Session, textMessage);
            
            MessageText = string.Empty;
            
            if (IsNewConversation)
            {
                IsNewConversation = false;
                NewConversationRecipients.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending message");
        }
    }
    public void StartNewConversation()
    {
        IsNewConversation = true;
        SelectedConversation = null;
        NewConversationRecipients.Clear();
        MessageText = string.Empty;
        OnPropertyChanged(nameof(ShouldShowEmptyState));
    }

    public void AddAddress(Contact contact)
    {
        NewConversationRecipients.Add(contact);
    }

    public void RemoveAddress(Contact contact)
    {
        NewConversationRecipients.Remove(contact);
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

        if (searchText.Length < 2)
            return;

        var filtered = Conversations
            .Where(c => c.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       c.LastMessage.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       c.Contacts.Any(s => s.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(c => c.LastMessageTimestamp) 
            .Take(10) 
            .ToList();

        SearchResults.AddRange(filtered);
    }

    public void SearchContacts(string searchText)
    {
        SearchContactsResults.Clear();

        if (string.IsNullOrWhiteSpace(searchText)) return;

        if (Contacts.Count == 0)
        {
            // if contacts are null, try loading contacts again
            LoadContacts();
        }

        var filtered = Contacts
            .Where(c => c.DisplayName != null && c.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       c.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.DisplayName)
            .Take(10)
            .ToList();

        SearchContactsResults.AddRange(filtered);
    }

    private void OnConversationsUpdated(object? sender, (string DeviceId, long ThreadId) args)
    {
        if (ActiveDevice?.Id == args.DeviceId)
        {
            dispatcher.EnqueueAsync(() =>
            {
                var updatedConversation = Conversations?.FirstOrDefault(c => c.ThreadId == args.ThreadId);
                if (updatedConversation != null && SelectedConversation != null && args.ThreadId == SelectedConversation.ThreadId)
                {
                    var newMessages = updatedConversation.Messages
                        .Where(m => !MessageIds.Contains(m.UniqueId))
                        .OrderBy(m => m.Timestamp)
                        .ToList();
                    HandleNewMessages(newMessages);
                }
                OnPropertyChanged(nameof(Conversations));
                return Task.CompletedTask;
            });
        }
    }

    private const int groupingThreshold = 300000; // 5 min
    private void BuildMessageGroups(List<Message> messages)
    {
        MessageGroup? currentGroup = null;

        foreach (var message in messages)
        {
            bool shouldStartNewGroup = currentGroup == null || !currentGroup.Sender.Address.Equals(message.Contact.Address, StringComparison.OrdinalIgnoreCase) ||
                currentGroup.IsReceived != (message.MessageType == 1) || (message.Timestamp - currentGroup.LatestTimestamp) > (groupingThreshold);

            if (shouldStartNewGroup)
            {
                currentGroup = new MessageGroup
                {
                    Sender = message.Contact,
                    Messages = []
                };
                MessageGroups.Add(currentGroup);
            }
            currentGroup?.Messages.Add(message);
        }
    }

    private void HandleNewMessages(List<Message> messages)
    {
        if (messages.Count == 0) return;

        MessageIds.AddRange(messages.Select(m => m.UniqueId));

        foreach (var message in messages.OrderBy(m => m.Timestamp))
        {
            AddMessageToGroups(message);
        }
    }
    
    private void AddMessageToGroups(Message message)
    {
        if (MessageGroups.Count == 0)
        {
            // First message - create new group
            MessageGroups.Add(new MessageGroup
            {
                Sender = message.Contact,
                Messages = [message]
            });
            return;
        }

        // if message belongs at the end of the last group
        var lastGroup = MessageGroups[^1];
        if (message.Timestamp >= lastGroup.LatestTimestamp)
        {
            if (CanGroupWith(message, lastGroup))
            {
                lastGroup.Messages.Add(message);
                return;
            }
            MessageGroups.Add(new MessageGroup
            {
                Sender = message.Contact,
                Messages = [message]
            });
            return;
        }

        // if message belongs at the beginning of the first group
        var firstGroup = MessageGroups[0];
        if (message.Timestamp <= firstGroup.Messages[0].Timestamp)
        {
            if (CanGroupWith(message, firstGroup))
            {
                firstGroup.Messages.Insert(0, message);
                return;
            }
            MessageGroups.Insert(0, new MessageGroup
            {
                Sender = message.Contact,
                Messages = [message]
            });
            return;
        }

        int insertIndex = FindGroupInsertionIndexBinary(message.Timestamp);
        
        if (TryAddToExistingGroup(message, insertIndex))
            return;

        MessageGroups.Insert(insertIndex, new MessageGroup
        {
            Sender = message.Contact,
            Messages = [message]
        });
    }

    private int FindGroupInsertionIndexBinary(long timestamp)
    {
        int left = 0, right = MessageGroups.Count;
        
        while (left < right)
        {
            int mid = left + (right - left) / 2;
            if (MessageGroups[mid].Messages[0].Timestamp <= timestamp)
                left = mid + 1;
            else
                right = mid;
        }
        
        return left;
    }

    private bool TryAddToExistingGroup(Message message, int insertIndex)
    {
        // Check previous group (most common case - newer messages)
        if (insertIndex > 0)
        {
            var prevGroup = MessageGroups[insertIndex - 1];
            if (CanGroupWith(message, prevGroup) && message.Timestamp >= prevGroup.LatestTimestamp)
            {
                prevGroup.Messages.Add(message);
                return true;
            }
        }

        // Check next group (for older messages or prepending)
        if (insertIndex < MessageGroups.Count)
        {
            var nextGroup = MessageGroups[insertIndex];
            if (CanGroupWith(message, nextGroup) && message.Timestamp <= nextGroup.Messages[0].Timestamp)
            {
                nextGroup.Messages.Insert(0, message);
                return true;
            }
        }

        return false;
    }

    private static bool CanGroupWith(Message message, MessageGroup group)
    {
        return group.Sender.Address.Equals(message.Contact.Address, StringComparison.OrdinalIgnoreCase) &&
               group.IsReceived == (message.MessageType == 1) &&
               Math.Abs(message.Timestamp - GetClosestTimestamp(message, group)) <= groupingThreshold;
    }

    private static long GetClosestTimestamp(Message message, MessageGroup group)
    {
        var firstTimestamp = group.Messages[0].Timestamp;
        var lastTimestamp = group.LatestTimestamp;
        
        return Math.Abs(message.Timestamp - firstTimestamp) <= Math.Abs(message.Timestamp - lastTimestamp)
            ? firstTimestamp 
            : lastTimestamp;
    }

}
