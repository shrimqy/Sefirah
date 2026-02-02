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
    public ObservableCollection<Conversation> Conversations { get; } = [];
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
            if (value is not null)
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

    [ObservableProperty]
    public partial PhoneNumber? SelectedPhoneNumber { get; set; } = null;

    public PairedDevice? ActiveDevice => deviceManager.ActiveDevice;

    public bool ShouldShowEmptyState => !IsNewConversation && SelectedConversation is null;
    public bool ShouldShowComposeUI => IsNewConversation || SelectedConversation is not null;
    #endregion

    public MessagesViewModel()
    {
        deviceManager.ActiveDeviceChanged += OnActiveDeviceChanged;
        smsHandlerService.ConversationRemoved += OnConversationRemoved;
        smsHandlerService.ConversationUpdated += OnConversationUpdated;
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        if (ActiveDevice is not null)
        {
            await LoadConversationsForActiveDevice();
        }
        else
        {
            await dispatcher.EnqueueAsync(() => Conversations.Clear());
        }
    }

    private void OnActiveDeviceChanged(object? sender, PairedDevice? _)
    {
        OnPropertyChanged(nameof(ActiveDevice));
        InitializeAsync();
        SelectedConversation = null;
        IsNewConversation = false;
    }

    private async void LoadContacts()
    {
        Contacts = await smsHandlerService.GetAllContactsAsync();
    }

    private async Task LoadConversationsForActiveDevice()
    {
        try
        {
            var conversations = await smsHandlerService.LoadConversationAsync(ActiveDevice!.Id);
            await dispatcher.EnqueueAsync(() =>
            {
                Conversations.Clear();
                Conversations.AddRange(conversations);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading conversations for device: {DeviceId}", ActiveDevice?.Id);
        }
    }

    private async void LoadMessagesForSelectedConversation()
    {
        if (SelectedConversation is null || ActiveDevice is null) return;

        var dbMessages = await smsHandlerService.LoadMessagesForConversation(ActiveDevice.Id, SelectedConversation.ThreadId);
        if (dbMessages.Count > 0)
        {
            MessageGroups.Clear();
            MessageIds.Clear();

            var sortedMessages = dbMessages.OrderBy(m => m.Timestamp).ToList();
            MessageIds.AddRange(sortedMessages.Select(m => m.UniqueId));
            BuildMessageGroups(sortedMessages);
        }

        // Request thread history from device
        await SmsHandlerService.RequestThreadHistory(ActiveDevice, SelectedConversation.ThreadId);
    }

    public void SendMessage(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText) || !ActiveDevice!.IsConnected) return;
        
        try
        {
            List<string> recipients = [];
            if (IsNewConversation)
            {
                recipients = NewConversationRecipients.Select(c => c.Address).ToList();
                if (recipients.Count == 0) return;
            }
            else if (SelectedConversation is not null)
            {
                recipients = SelectedConversation.Contacts.Select(s => s.Address).ToList();
            }
            else
            {
                return;
            }

            var textMessage = new TextMessage
            {
                Body = messageText.Trim(),
                ThreadId = SelectedConversation?.ThreadId ?? -1,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MessageType = 2, // SENT
                Read = true,
                UniqueId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SubscriptionId = SelectedPhoneNumber?.SubscriptionId ?? -1,
                Addresses = recipients
            };

            ActiveDevice?.SendMessage(textMessage);
            
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
        
        if (string.IsNullOrWhiteSpace(searchText)) return;

        if (searchText.Length < 2) return;

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
            .Where(c => c.DisplayName is not null && c.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       c.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.DisplayName)
            .Take(10)
            .ToList();

        SearchContactsResults.AddRange(filtered);
    }

    private void OnConversationRemoved(object? sender, (string DeviceId, long ThreadId) args)
    {
        if (ActiveDevice?.Id != args.DeviceId) return;

        dispatcher.EnqueueAsync(() =>
        {
            var toRemove = Conversations.FirstOrDefault(c => c.ThreadId == args.ThreadId);
            if (toRemove is not null)
                Conversations.Remove(toRemove);
            if (SelectedConversation?.ThreadId == args.ThreadId)
            {
                MessageGroups.Clear();
                MessageIds.Clear();
                SelectedConversation = null;
                OnPropertyChanged(nameof(ShouldShowEmptyState));
                OnPropertyChanged(nameof(ShouldShowComposeUI));
            }
        });
    }

    private void OnConversationUpdated(object? sender, (string DeviceId, long ThreadId, Conversation Conversation, IReadOnlyList<Message> NewMessages) args)
    {
        if (ActiveDevice?.Id != args.DeviceId) return;

        dispatcher.EnqueueAsync(() =>
        {
            var conversation = args.Conversation;
            var existing = Conversations.FirstOrDefault(c => c.ThreadId == args.ThreadId);
            if (existing is not null)
            {
                existing.UpdateFrom(conversation);
                var currentIndex = Conversations.IndexOf(existing);
                var targetIndex = FindConversationInsertionIndex(Conversations, existing.LastMessageTimestamp);
                if (currentIndex != targetIndex)
                    Conversations.Move(currentIndex, targetIndex);
            }
            else
            {
                var index = FindConversationInsertionIndex(Conversations, conversation.LastMessageTimestamp);
                Conversations.Insert(index, conversation);
            }

            if (SelectedConversation?.ThreadId == args.ThreadId && args.NewMessages.Count > 0)
            {
                var newMessages = args.NewMessages.Where(m => !MessageIds.Contains(m.UniqueId)).OrderBy(m => m.Timestamp).ToList();
                if (newMessages.Count > 0)
                    HandleNewMessages(newMessages);
            }
        });
    }

    private static int FindConversationInsertionIndex(ObservableCollection<Conversation> conversations, long lastMessageTimestamp)
    {
        for (int i = 0; i < conversations.Count; i++)
        {
            if (conversations[i].LastMessageTimestamp <= lastMessageTimestamp)
                return i;
        }
        return conversations.Count;
    }

    private const int groupingThreshold = 300000; // 5 min
    private void BuildMessageGroups(List<Message> messages)
    {
        MessageGroup? currentGroup = null;

        foreach (var message in messages)
        {
            bool shouldStartNewGroup = currentGroup is null || !currentGroup.Sender.Address.Equals(message.Contact.Address, StringComparison.OrdinalIgnoreCase) ||
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
