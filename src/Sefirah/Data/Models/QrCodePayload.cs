namespace Sefirah.Data.Models;

public sealed class QrCodePayload
{
    public List<string> Addresses { get; set; } = [];

    public int Port { get; set; }

    public required string DeviceId { get; set; }

    public required string DeviceName { get; set; }
}
