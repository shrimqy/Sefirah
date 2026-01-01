namespace Sefirah.Data.EventArguments;

public sealed class DiscoveredMdnsServiceArgs(string deviceId, string deviceName, string publicKey, string ipAddress, int port) : EventArgs
{
    public string DeviceId { get; } = deviceId;
    public string DeviceName { get; } = deviceName;
    public string PublicKey { get; } = publicKey;
    public string IpAddress { get; } = ipAddress;
    public int Port { get; } = port;
}
