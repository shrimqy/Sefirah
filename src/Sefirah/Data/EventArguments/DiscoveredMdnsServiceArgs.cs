namespace Sefirah.Data.EventArguments;

public class DiscoveredMdnsServiceArgs : EventArgs
{
    public required string DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}
