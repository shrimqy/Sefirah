using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

/// <summary>
/// One thing the device put on its clipboard. Android keeps no history of its own, so this table
/// is the only record of what was copied before the current item.
/// </summary>
public class ClipboardEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Mime type as the device reported it, or text/plain for plain text.</summary>
    public string ClipboardType { get; set; } = string.Empty;

    /// <summary>The text itself, or the path of the stored image.</summary>
    public string Content { get; set; } = string.Empty;

    public bool IsImage { get; set; }

    public long TimestampMillis { get; set; }
}
