using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class MessageEntity
{
    [PrimaryKey]
    public long UniqueId { get; set; }

    [Indexed]
    public long ThreadId { get; set; }

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public long Timestamp { get; set; }

    public bool Read { get; set; }

    public int SubscriptionId { get; set; } = 0;

    public int MessageType { get; set; } = 1; // 1 = INBOX, 2 = SENT

    public string Address { get; set; } = string.Empty;

    [Ignore]
    public List<SmsAttachment> Attachments { get; set; } = [];

    #region Helpers
    public static MessageEntity FromMessage(TextMessage message, string deviceId) => new()
    {
        UniqueId = message.UniqueId,
        ThreadId = message.ThreadId,
        DeviceId = deviceId,
        Body = message.Body,
        Timestamp = message.Timestamp,
        Read = message.Read,
        SubscriptionId = message.SubscriptionId,
        MessageType = message.MessageType,
        Address = message.Addresses[0]
    };

    internal async Task<Message> ToMessageAsync(ContactRepository contactRepository)
    {
        var contact = await contactRepository.GetContactAsync(DeviceId, Address);
        var participant = contact is not null ? await contact.ToParticipantInfo() : new ParticipantInfo(Address, Address);

        return new Message
        {
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
