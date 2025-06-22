using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class SmsConversationEntity
{
    [PrimaryKey]
    public long ThreadId { get; set; }

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public long LastMessageTimestamp { get; set; }

    public string? LastMessageBody { get; set; }

    public bool HasRead { get; set; }

    public long TimeStamp { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}
