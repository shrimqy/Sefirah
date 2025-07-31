using SQLite;

namespace Sefirah.Data.AppDatabase.Models;
public class AttachmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public long MessageUniqueId { get; set; }

    public byte[]? Data { get; set; }
}
