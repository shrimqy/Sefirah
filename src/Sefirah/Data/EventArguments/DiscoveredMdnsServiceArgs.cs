namespace Sefirah.Data.EventArguments;

public sealed class DiscoveredMdnsServiceArgs(string deviceId, string deviceName, string address, int port) : EventArgs
{
    public string DeviceId { get; } = deviceId;
    public string DeviceName { get; } = deviceName;
    public string Address { get; } = address;
    public int Port { get; } = port;
}
