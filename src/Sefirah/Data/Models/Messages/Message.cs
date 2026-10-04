namespace Sefirah.Data.Models.Messages;

public partial class Message : ObservableObject
{
    public string MessageKey { get; set; } = string.Empty;

    public long UniqueId { get; set; }

    public Contact Participant { get; set; } = null!;

    public long ThreadId { get; set; }

    public string Body { get; set; } = string.Empty;

    public long Timestamp { get; set; }

    public int MessageType { get; set; }

    public bool Read { get; set; }

    public int SubscriptionId { get; set; }

    public List<MessageAttachment> Attachments { get; set; } = [];

    // Text visibility comes from the body, including text carried by MMS.
    public bool HasBody => !string.IsNullOrWhiteSpace(Body);

    public bool HasAttachments => Attachments.Count > 0;

    private OutgoingSendState sendState;
    public OutgoingSendState SendState
    {
        get => sendState;
        set => SetProperty(ref sendState, value);
    }

    private bool showSendIndicator;
    public bool ShowSendIndicator
    {
        get => showSendIndicator;
        set => SetProperty(ref showSendIndicator, value);
    }

    private bool showSendCheck;
    public bool ShowSendCheck
    {
        get => showSendCheck;
        set => SetProperty(ref showSendCheck, value);
    }

    public static string PreviewText(string? body, IEnumerable<string?>? mimeTypes)
    {
        if (!string.IsNullOrWhiteSpace(body))
            return body;

        var mime = mimeTypes?.FirstOrDefault(m => !string.IsNullOrEmpty(m));
        return string.IsNullOrEmpty(mime) ? string.Empty : AttachmentBase.TypeLabel(mime);
    }
}
