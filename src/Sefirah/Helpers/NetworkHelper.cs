using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Sefirah.Helpers;

public static class NetworkHelper
{

    public static async Task<int> FindAvailablePortAsync(int startPort)
    {
        int port = startPort;
        const int maxPortNumber = 65535;

        while (port <= maxPortNumber)
        {
            try
            {
                using var testListener = new TcpListener(IPAddress.Any, port);
                await Task.Run(() =>
                {
                    testListener.Start();
                    testListener.Stop();
                });

                return port;
            }
            catch (SocketException)
            {
                port++;
            }
        }
        return port;
    }

    public static List<IPAddressInfo> GetAllValidAddresses()
    {
        var addresses = new List<IPAddressInfo>();
        
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet &&
                ni.OperationalStatus == OperationalStatus.Up)
            {
                var gateway = ni.GetIPProperties().GatewayAddresses
                    .FirstOrDefault(g => g.Address?.AddressFamily == AddressFamily.InterNetwork)?
                    .Address;

                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork && 
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
