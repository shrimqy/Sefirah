namespace Sefirah.Data.EventArguments;

public sealed class DiscoveredMdnsServiceArgs(string deviceId, string deviceName, string publicKey) : EventArgs
{
    public string DeviceId { get; } = deviceId;
    public string DeviceName { get; } = deviceName;
    public string PublicKey { get; } = publicKey;
}
