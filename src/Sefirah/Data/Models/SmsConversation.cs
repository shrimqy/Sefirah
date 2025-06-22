using System.Collections.Specialized;

namespace Sefirah.Data.Models;
public partial class SmsConversation : ObservableObject
{
    public long ThreadId { get; set; }

    public ObservableCollection<TextMessage> Messages { get; set; } = [];

    private string lastMessage = string.Empty;
    public string LastMessage
    {
        get => lastMessage;
        set
        {
            if (lastMessage != value)
            {
                SetProperty(ref lastMessage, value);
            }
        }
    }

    private long lastMessageTimestamp;
    public long LastMessageTimestamp
    {
        get => lastMessageTimestamp;
        set
        {
            if (lastMessageTimestamp != value)
            {
                SetProperty(ref lastMessageTimestamp, value);
            }
        }
    }

    private string displayName = string.Empty;
    public string DisplayName
    {
        get => displayName;
        set
        {
            if (displayName != value)
            {
                SetProperty(ref displayName, value);
            }
        }
    }

    // Parameterless constructor for database loading
    public SmsConversation()
    {
        Messages.CollectionChanged += Messages_CollectionChanged;
    }

    public SmsConversation(TextConversation textConversation)
    {
        ThreadId = textConversation.ThreadId;

        Messages.CollectionChanged += Messages_CollectionChanged;

        if (textConversation.Messages != null && textConversation.Messages.Count > 0)
        {
            // Sort messages
            var sortedMessages = textConversation.Messages
                .OrderBy(m => m.Timestamp)
                .ToList();

            foreach (var message in sortedMessages)
            {
                Messages.Add(message);
            }

            // Set initial snippet and timestamp from the latest message
            var latestMessage = textConversation.Messages.OrderByDescending(m => m.Timestamp).FirstOrDefault();
            if (latestMessage != null)
            {
                LastMessage = latestMessage.Body;
                LastMessageTimestamp = latestMessage.Timestamp;
            }

            var displayName = textConversation.Messages.FirstOrDefault()?.Contacts.FirstOrDefault()?.ContactName ?? string.Empty;
            if (displayName == string.Empty)
            {
                displayName = textConversation.Messages.FirstOrDefault()?.Addresses.FirstOrDefault() ?? string.Empty;
            }
            DisplayName = displayName;
        }
    }

    public void UpdateFromTextConversation(TextConversation textConversation)
    {
        if (textConversation.ThreadId != ThreadId)
        {
            throw new ArgumentException($"Thread ID mismatch: {textConversation.ThreadId} vs {ThreadId}");
        }

        var existingMessageIds = new HashSet<long>(Messages.Select(m => m.UniqueId));
        var incomingMessageIds = new HashSet<long>(textConversation.Messages.Select(m => m.UniqueId));

        // Find messages that exist locally but not in the incoming conversation (deleted remotely)
        var messagesToRemove = Messages.Where(m => !incomingMessageIds.Contains(m.UniqueId)).ToList();
        foreach (var message in messagesToRemove)
        {
            Messages.Remove(message);
        }

        // Only add messages that don't already exist in our collection
        var newMessages = textConversation.Messages
            .Where(message => !existingMessageIds.Contains(message.UniqueId))
            .ToList();

        foreach (var message in newMessages)
        {
            int insertIndex = 0;

            while (insertIndex < Messages.Count &&
                   Messages[insertIndex].Timestamp < message.Timestamp)
            {
                insertIndex++;
            }

            Messages.Insert(insertIndex, message);
        }
    }

    public void NewMessageFromConversation(TextConversation textConversation)
    {
        // Track existing message IDs
        var existingMessageIds = new HashSet<long>(Messages.Select(m => m.UniqueId));

        // Only add messages that don't already exist in our collection
        var newMessages = textConversation.Messages
            .Where(message => !existingMessageIds.Contains(message.UniqueId))
            .ToList();

        foreach (var message in newMessages)
        {
            int insertIndex = 0;

            while (insertIndex < Messages.Count &&
                   Messages[insertIndex].Timestamp < message.Timestamp)
            {
                insertIndex++;
            }

            Messages.Insert(insertIndex, message);
        }
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (TextMessage message in e.NewItems)
            {
                // Update snippet and timestamp if this is a newer message
                if (message.Timestamp > LastMessageTimestamp)
                {
                    LastMessage = message.Body;
                    LastMessageTimestamp = message.Timestamp;
                }
            }
        }
    }
}
