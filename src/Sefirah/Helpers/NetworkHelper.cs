using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Sefirah.Helpers;

public static class NetworkHelper
{
    /// <summary>
    /// 169.254.0.0/16 is what Windows hands to an adapter whose DHCP failed. Announcing those, or
    /// putting them in the pairing QR, only makes the other device work through timeouts before it
    /// reaches the address that actually routes.
    /// </summary>
    private static bool IsLinkLocal(IPAddress address)
    {
        var octets = address.GetAddressBytes();
        return octets[0] == 169 && octets[1] == 254;
    }

    public static List<IPAddressInfo> GetAllValidAddresses()
    {
        var addresses = new List<IPAddressInfo>();
        
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus is OperationalStatus.Up)
            {
                var gateway = ni.GetIPProperties().GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily is AddressFamily.InterNetwork)?.Address;

                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily is AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip.Address) &&
                        !IsLinkLocal(ip.Address))
                    {
                        addresses.Add(new IPAddressInfo(
                            Address: ip.Address,
                            SubnetMask: ip.IPv4Mask,
                            Gateway: gateway
                        ));
                    }
                }
            }
        }
        
        return addresses;
    }

    public record IPAddressInfo(IPAddress Address, IPAddress SubnetMask, IPAddress? Gateway);
}
