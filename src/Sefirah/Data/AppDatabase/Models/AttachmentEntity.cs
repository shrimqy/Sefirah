using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class AttachmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public long MessageUniqueId { get; set; }

    public byte[]? Data { get; set; }

    public static AttachmentEntity FromAttachment(SmsAttachment attachment, long messageUniqueId) => new()
    {
        MessageUniqueId = messageUniqueId,
        Data = Convert.FromBase64String(attachment.Base64EncodedFile!)
    };
}
