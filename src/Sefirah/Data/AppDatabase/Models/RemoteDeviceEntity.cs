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

    [Column("IpAddresses")]
    public string? IpAddressesJson { get; set; }
    
    [Ignore]
    public List<IpAddressEntry> IpAddresses
    {
        get
        {
            if (string.IsNullOrEmpty(IpAddressesJson)) return [];

            var entries = JsonSerializer.Deserialize<List<IpAddressEntry>>(IpAddressesJson);
            if (entries is not null)
                return entries;

            return [];
        }
        set => IpAddressesJson = value is null ? null : JsonSerializer.Serialize(value);
    }

    [Column("PhoneNumbers")]
    public string? PhoneNumbersJson { get; set; }

    [Ignore]
    public List<PhoneNumber>? PhoneNumbers
    {
        get => string.IsNullOrEmpty(PhoneNumbersJson) ? null : JsonSerializer.Deserialize<List<PhoneNumber>>(PhoneNumbersJson);
        set => PhoneNumbersJson = value is null ? null : JsonSerializer.Serialize(value);
    }

    #region Helpers
    internal async Task<PairedDevice> ToPairedDevice()
    {
        return new PairedDevice(DeviceId)
        {
            Name = Name,
            Model = Model,
            IpAddresses = IpAddresses,
            PhoneNumbers = PhoneNumbers,
            Wallpaper = await ImageHelper.ToBitmapAsync(WallpaperBytes),
        };
    }
    #endregion
}
