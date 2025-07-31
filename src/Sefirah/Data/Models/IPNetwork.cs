using System.Net;

namespace Sefirah.Data.Models;
public class IPNetwork(IPAddress address, IPAddress mask)
{
    public IPAddress Address { get; } = address;
    public IPAddress Mask { get; } = mask;

    public IPAddress BroadcastAddress
    {
        get
        {
            var ipBytes = Address.GetAddressBytes();
            var maskBytes = Mask.GetAddressBytes();
            var broadcastBytes = new byte[ipBytes.Length];

            for (int i = 0; i < ipBytes.Length; i++)
            {
                broadcastBytes[i] = (byte)(ipBytes[i] | (byte)~maskBytes[i]);
            }

            return new IPAddress(broadcastBytes);
        }
    }
}
