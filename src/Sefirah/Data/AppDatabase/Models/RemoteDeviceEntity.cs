using Sefirah.Data.Models;
using Sefirah.Helpers;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public partial class RemoteDeviceEntity
{
    [PrimaryKey]
    public string DeviceId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public byte[]? SharedSecret { get; set; }

    public byte[]? WallpaperBytes { get; set; }

    public DateTime? LastConnected { get; set; }

    [Column("IpAddresses")]
    public string? IpAddressesJson { get; set; }
    
    [Ignore]
    public List<string> IpAddresses
    {
        get => string.IsNullOrEmpty(IpAddressesJson) ? [] : JsonSerializer.Deserialize<List<string>>(IpAddressesJson) ?? [];
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
