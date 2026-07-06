using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class ConversationEntity
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public long ThreadId { get; set; }

    public string? AddressesJson { get; set; }

    public long LastMessageTimestamp { get; set; }

    public string? LastMessage { get; set; }

    public bool HasRead { get; set; }

    public long TimeStamp { get; set; }

    [Ignore]
    public List<string> Addresses { get; set; } = [];

    #region Helpers
    public static string GetKey(string deviceId, long threadId) => $"{deviceId}:{threadId}";

    public static ConversationEntity FromMessage(ConversationInfo thread, string deviceId)
    {
        var latestMessage = thread.Messages.OrderByDescending(m => m.Timestamp).First();
        return new ConversationEntity
        {
            Key = GetKey(deviceId, thread.ThreadId),
            DeviceId = deviceId,
            ThreadId = thread.ThreadId,
            AddressesJson = JsonSerializer.Serialize(thread.Recipients),
            HasRead = thread.Messages.Any(m => m.Read),
            TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastMessageTimestamp = latestMessage.Timestamp,
            LastMessage = latestMessage.Body
        };
    }

    internal Conversation ToConversation(ContactRepository contactRepository)
    {
        if (!string.IsNullOrEmpty(AddressesJson))
            Addresses = JsonSerializer.Deserialize<List<string>>(AddressesJson) ?? [];

        var contacts = new List<Contact>();
        foreach (var address in Addresses)
        {
            var contact = contactRepository.GetContact(DeviceId, address);
            if (!contacts.Contains(contact))
                contacts.Add(contact);
        }

        return new Conversation
        {
            ConversationKey = Key,
            ThreadId = ThreadId,
            Contacts = contacts,
            LastMessage = LastMessage ?? string.Empty,
            LastMessageTimestamp = LastMessageTimestamp,
            HasRead = HasRead,
        };
    }
    #endregion
}
