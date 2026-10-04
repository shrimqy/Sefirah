using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class AttachmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string MessageKey { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long PartId { get; set; } = -1;
}
