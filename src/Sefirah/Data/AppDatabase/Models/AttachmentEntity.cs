using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class AttachmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string MessageKey { get; set; } = string.Empty;

    public byte[]? Data { get; set; }

    public static AttachmentEntity FromAttachment(SmsAttachment attachment, string messageKey) => new()
    {
        MessageKey = messageKey,
        Data = Convert.FromBase64String(attachment.Base64EncodedFile!)
    };
}
