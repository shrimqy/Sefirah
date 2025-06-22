using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class TextMessageEntity
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

    public string? AddressesJson { get; set; }

    public string? ContactsJson { get; set; }

    [Ignore]
    public List<string> Addresses { get; set; } = [];

    [Ignore]
    public List<Contact> Contacts { get; set; } = [];

    [Ignore]
    public List<SmsAttachment> Attachments { get; set; } = [];
}
