using SQLite;

namespace Sefirah.Data.AppDatabase.Models;
public class SmsAttachmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public long MessageUniqueId { get; set; }

    public byte[]? Data { get; set; }
}
