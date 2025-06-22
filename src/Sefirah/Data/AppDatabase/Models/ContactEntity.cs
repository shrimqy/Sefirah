using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class ContactEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public string ContactName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public byte[]? Avatar { get; set; }
} 