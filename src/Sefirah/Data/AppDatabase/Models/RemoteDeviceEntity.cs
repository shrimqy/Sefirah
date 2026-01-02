using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Helpers;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class RemoteDeviceEntity
{
    [PrimaryKey]
    public string DeviceId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public byte[] SharedSecret { get; set; } = null!;

    public byte[]? WallpaperBytes { get; set; }

    public DateTime? LastConnected { get; set; }

    [Column("Addresses")]
    public string? AddressesJson { get; set; }
    
    [Ignore]
    public List<AddressEntry> Addresses
    {
        get => string.IsNullOrEmpty(AddressesJson) ? [] : JsonSerializer.Deserialize<List<AddressEntry>>(AddressesJson) ?? [];

        set => AddressesJson = value is null ? null : JsonSerializer.Serialize(value);
    }

    [Column("PhoneNumbers")]
    public string? PhoneNumbersJson { get; set; }

    [Ignore]
    public List<PhoneNumber> PhoneNumbers
    {
        get => string.IsNullOrEmpty(PhoneNumbersJson) ? [] : JsonSerializer.Deserialize<List<PhoneNumber>>(PhoneNumbersJson) ?? [];
        set => PhoneNumbersJson = value is null ? null : JsonSerializer.Serialize(value);
    }

    #region Helpers
    internal async Task<PairedDevice> ToPairedDevice()
    {
        return new PairedDevice(DeviceId)
        {
            Name = Name,
            Model = Model,
            Addresses = Addresses,
            PhoneNumbers = PhoneNumbers,
            Wallpaper = await ImageHelper.ToBitmapAsync(WallpaperBytes),
        };
    }
    #endregion
}
