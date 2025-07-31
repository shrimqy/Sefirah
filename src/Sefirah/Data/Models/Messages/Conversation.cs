using System.Collections.Specialized;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;

namespace Sefirah.Data.Models.Messages;

public partial class Conversation : ObservableObject
{
    public Conversation()
    {
        Messages.CollectionChanged += Messages_CollectionChanged;
    }

    public long ThreadId { get; set; }
    public ObservableCollection<Message> Messages { get; set; } = [];
    public List<Contact> Contacts { get; set; } = [];
    public string? AvatarGlyph { get; set; } = string.Empty;
    public BitmapImage? AvatarImage { get; set; }
    public string DisplayName => Contacts.Count != 0 ? string.Join(", ", Contacts.Select(s => !string.IsNullOrEmpty(s.DisplayName) ? s.DisplayName : s.Address)) : "Unknown";


    private string lastMessage = string.Empty;
    public string LastMessage
    {
        get => lastMessage;
        set => SetProperty(ref lastMessage, value);
    }

    private long lastMessageTimestamp;
    public long LastMessageTimestamp
    {
        get => lastMessageTimestamp;
        set => SetProperty(ref lastMessageTimestamp, value);
    }

    #region Helpers
    public async Task UpdateFromTextConversationAsync(TextConversation textConversation, SmsRepository repository, string deviceId)
    {
        if (textConversation.ThreadId != ThreadId)
        {
            throw new ArgumentException($"Thread ID mismatch: {textConversation.ThreadId} vs {ThreadId}");
        }

        if(textConversation.Recipients.Count > 0)
        {
            // Look up contact information for each recipient
            var updatedContacts = new List<Contact>();
            foreach (var recipient in textConversation.Recipients)
            {
                var contactEntity = await repository.GetContactAsync(deviceId, recipient);
                var contact = contactEntity != null ? await contactEntity.ToContact() : new Contact(recipient, recipient);
                updatedContacts.Add(contact);
            }
            
            Contacts = updatedContacts;
            OnPropertyChanged(nameof(DisplayName));
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
        var newMessages = new List<Message>();
        foreach (var tm in textConversation.Messages.Where(message => !existingMessageIds.Contains(message.UniqueId)))
        {
            var contactAddress = tm.Addresses[0];
            var contactEntity = await repository.GetContactAsync(deviceId, contactAddress);
            var contact = contactEntity != null ? await contactEntity.ToContact() : new Contact(contactAddress, contactAddress);

            var message = new Message
            {
                UniqueId = tm.UniqueId,
                ThreadId = tm.ThreadId,
                Body = tm.Body,
                Timestamp = tm.Timestamp,
                MessageType = tm.MessageType,
                Read = tm.Read,
                SubscriptionId = tm.SubscriptionId,
                Attachments = tm.Attachments,
                Contact = contact,
                IsTextMessage = tm.IsTextMessage
            };

            newMessages.Add(message);
        }

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

    public async Task NewMessageFromConversationAsync(TextConversation textConversation, SmsRepository repository, string deviceId)
    {
        // Track existing message IDs
        var existingMessageIds = new HashSet<long>(Messages.Select(m => m.UniqueId));

        // Only add messages that don't already exist in our collection
        var newMessages = new List<Message>();
        foreach (var tm in textConversation.Messages.Where(message => !existingMessageIds.Contains(message.UniqueId)))
        {
            var contactAddress = tm.Addresses[0];
            var contactEntity = await repository.GetContactAsync(deviceId, contactAddress);
            var contact = contactEntity != null ? await contactEntity.ToContact() : new Contact(contactAddress, contactAddress);

            var message = new Message
            {
                UniqueId = tm.UniqueId,
                ThreadId = tm.ThreadId,
                Body = tm.Body,
                Timestamp = tm.Timestamp,
                MessageType = tm.MessageType,
                Read = tm.Read,
                SubscriptionId = tm.SubscriptionId,
                Attachments = tm.Attachments,
                Contact = contact,
                IsTextMessage = tm.IsTextMessage
            };

            newMessages.Add(message);
        }

        if (textConversation.Recipients.Count > 0)
        {
            // Look up contact information for each recipient
            var updatedContacts = new List<Contact>();
            foreach (var recipient in textConversation.Recipients)
            {
                var contactEntity = await repository.GetContactAsync(deviceId, recipient);
                var contact = contactEntity != null ? await contactEntity.ToContact() : new Contact(recipient, recipient);
                updatedContacts.Add(contact);
            }
            
            Contacts = updatedContacts;
            OnPropertyChanged(nameof(DisplayName));
        }
        
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
            foreach (Message message in e.NewItems)
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

    internal ConversationEntity ToConversationEntity(string deviceId)
    {
        return new ConversationEntity
        {
            ThreadId = ThreadId,
            DeviceId = deviceId,
            AddressesJson = JsonSerializer.Serialize(Contacts.Select(s => s.Address).ToList()),
            HasRead = Messages.Any(m => m.Read),
            TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
    #endregion
}
