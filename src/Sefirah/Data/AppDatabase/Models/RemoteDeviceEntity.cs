using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public partial class RemoteDeviceEntity
{
    [PrimaryKey]
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? Model { get; set; } = string.Empty;

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

    #region Helpers
    internal async Task<PairedDevice> ToPairedDevice()
    {
        var wallPaperBytes = WallpaperBytes;
        BitmapImage? wallPaper = null;

        if (wallPaperBytes != null)
        {
            var dispatcher = App.MainWindow?.DispatcherQueue;
            if (dispatcher != null)
            {
                await dispatcher.EnqueueAsync(async () =>
                {
                    wallPaper = await ImageHelper.ToBitmapAsync(wallPaperBytes);
                });
            }
        }

        return new PairedDevice(DeviceId)
        {
            Name = Name,
            Model = Model,
            IpAddresses = IpAddresses,
            PhoneNumbers = PhoneNumbers,
            Wallpaper = wallPaper,
        };
    }
    #endregion
}
