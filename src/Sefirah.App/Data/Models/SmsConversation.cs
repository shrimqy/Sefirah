using System.Collections.Specialized;

namespace Sefirah.App.Data.Models;

public partial class SmsConversation : INotifyPropertyChanged
{
    public long ThreadId { get; }
    
    public ObservableCollection<TextMessage> Messages { get; } = new();

    private string _snippet = string.Empty;
    public string Snippet 
    {
        get => _snippet;
        set
        {
            if (_snippet != value)
            {
                _snippet = value;
                OnPropertyChanged(nameof(Snippet));
            }
        }
    }

    private long _lastMessageTimestamp;
    public long LastMessageTimestamp
    {
        get => _lastMessageTimestamp;
        set
        {
            if (_lastMessageTimestamp != value)
            {
                _lastMessageTimestamp = value;
                OnPropertyChanged(nameof(LastMessageTimestamp));
            }
        }
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
                Snippet = latestMessage.Body;
                LastMessageTimestamp = latestMessage.Timestamp;
            }
        }
    }

    // Update this conversation with new messages from a TextConversation
    public void UpdateFromTextConversation(TextConversation textConversation)
    {
        if (textConversation.ThreadId != ThreadId)
        {
            throw new ArgumentException($"Thread ID mismatch: {textConversation.ThreadId} vs {ThreadId}");
        }
        
        // Track existing message IDs
        var existingMessageIds = new HashSet<long>(Messages.Select(m => m.UniqueId));
        
        // Track incoming message IDs
        var incomingMessageIds = new HashSet<long>(textConversation.Messages.Select(m => m.UniqueId));
        
        // Find messages that exist locally but not in the incoming conversation (deleted remotely)
        var messagesToRemove = Messages.Where(m => !incomingMessageIds.Contains(m.UniqueId)).ToList();
        
        // Remove messages that were deleted remotely
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
                    Snippet = message.Body;
                    LastMessageTimestamp = message.Timestamp;
                }
            }
        }
    }

    #region INotifyPropertyChanged Implementation
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion
}
