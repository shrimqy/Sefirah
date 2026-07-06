using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class MessageEntity
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [Indexed]
    public string ConversationKey { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public long UniqueId { get; set; }

    public long ThreadId { get; set; }

    public string Body { get; set; } = string.Empty;

    public long Timestamp { get; set; }

    public bool Read { get; set; }

    public int SubscriptionId { get; set; } = 0;

    public int MessageType { get; set; } = 1; // 1 = INBOX, 2 = SENT

    public string Address { get; set; } = string.Empty;

    [Ignore]
    public List<SmsAttachment> Attachments { get; set; } = [];

    #region Helpers
    public static string GetKey(string deviceId, long uniqueId) => $"{deviceId}:{uniqueId}";

    public static MessageEntity FromMessage(TextMessage message, string deviceId) => new()
    {
        Key = GetKey(deviceId, message.UniqueId),
        ConversationKey = ConversationEntity.GetKey(deviceId, message.ThreadId),
        DeviceId = deviceId,
        UniqueId = message.UniqueId,
        ThreadId = message.ThreadId,
        Body = message.Body,
        Timestamp = message.Timestamp,
        Read = message.Read,
        SubscriptionId = message.SubscriptionId,
        MessageType = message.MessageType,
        Address = message.Addresses.Count > 0 ? message.Addresses[0] : string.Empty
    };

    internal Message ToMessage(ContactRepository contactRepository)
    {
        var participant = contactRepository.GetContact(DeviceId, Address);

        return new Message
        {
            MessageKey = Key,
            UniqueId = UniqueId,
            ThreadId = ThreadId,
            Body = Body,
            Timestamp = Timestamp,
            Read = Read,
            SubscriptionId = SubscriptionId,
            MessageType = MessageType,
            Attachments = Attachments,
            Participant = participant,
        };
    }
    #endregion
}
