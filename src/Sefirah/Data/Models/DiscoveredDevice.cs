namespace Sefirah.Data.Models;

public class DiscoveredDevice(
    string deviceId, 
    string publicKey, 
    string deviceName, 
    byte[]? hashedKey, 
    DateTimeOffset lastSeen, 
    DeviceOrigin origin)
{
    public string DeviceId { get; } = deviceId;
    public string PublicKey { get; } = publicKey;
    public string DeviceName { get; } = deviceName;
    public byte[]? HashedKey { get; } = hashedKey;
    public DateTimeOffset LastSeen { get; } = lastSeen;
    public DeviceOrigin Origin { get; } = origin;

    public string? FormattedKey
    {
        get
        {
            if (HashedKey == null)
            {
                return "000000";
            }
            var derivedKeyInt = BitConverter.ToInt32(HashedKey, 0);
            derivedKeyInt = Math.Abs(derivedKeyInt) % 1_000_000;
            return derivedKeyInt.ToString().PadLeft(6, '0');
        }
    }
}

public enum DeviceOrigin
{
    MdnsService,
    UdpBroadcast
}
