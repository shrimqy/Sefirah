using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Sefirah.Helpers;

public static class NetworkHelper
{
    public static List<IPAddressInfo> GetAllValidAddresses()
    {
        var addresses = new List<IPAddressInfo>();
        
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet &&
                ni.OperationalStatus is OperationalStatus.Up)
            {
                var gateway = ni.GetIPProperties().GatewayAddresses
                    .FirstOrDefault(g => g.Address?.AddressFamily == AddressFamily.InterNetwork)?
                    .Address;

                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily is AddressFamily.InterNetwork && 
                        !IPAddress.IsLoopback(ip.Address))
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
