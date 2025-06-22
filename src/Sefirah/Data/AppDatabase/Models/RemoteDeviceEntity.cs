using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public partial class RemoteDeviceEntity
{
    [PrimaryKey]
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    [ColumnAttribute("IpAddresses")]
    public string? IpAddressesJson { get; set; }
    
    [Ignore]
    public List<string>? IpAddresses
    {
        get => string.IsNullOrEmpty(IpAddressesJson) ? null : JsonSerializer.Deserialize<List<string>>(IpAddressesJson);
        set => IpAddressesJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    [ColumnAttribute("PhoneNumbers")]
    public string? PhoneNumbersJson { get; set; }

    [Ignore]
    public List<PhoneNumber>? PhoneNumbers
    {
        get => string.IsNullOrEmpty(PhoneNumbersJson) ? null : JsonSerializer.Deserialize<List<PhoneNumber>>(PhoneNumbersJson);
        set => PhoneNumbersJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    public byte[]? SharedSecret { get; set; }
    public byte[]? WallpaperBytes { get; set; }

    public DateTime? LastConnected { get; set; }
}
