namespace Sefirah.Data.Models.Messages;

public class Message
{
    public string MessageKey { get; set; } = string.Empty;

    public long UniqueId { get; set; }

    public Contact Participant { get; set; } = null!;

    public long ThreadId { get; set; }

    public string Body { get; set; } = string.Empty;

    public long Timestamp { get; set; }

    public int MessageType { get; set; }

    public bool Read { get;  set; }

    public int SubscriptionId { get; set; }

    public List<SmsAttachment>? Attachments { get; set; }

    public bool IsTextMessage { get; set; }
} 
