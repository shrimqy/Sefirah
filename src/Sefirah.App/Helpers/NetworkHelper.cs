using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Sefirah.App.Utils;

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

    public static List<string> GetAllValidAddresses()
    {
        var ipv4Addresses = new List<string>();
        var ipv6Addresses = new List<string>();
        
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if ((ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || 
                 ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                ni.OperationalStatus == OperationalStatus.Up)
            {
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (IPAddress.IsLoopback(ip.Address)) 
                        continue;
                    
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipv4Addresses.Add(ip.Address.ToString());
                    }
                    else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        var ipString = ip.Address.IsIPv6LinkLocal 
                            ? $"{ip.Address}%{ip.Address.ScopeId}"
                            : ip.Address.ToString();
                            
                        ipv6Addresses.Add(ipString);
                    }
                }
            }
        }
        
        
        return ipv4Addresses.Concat(ipv6Addresses).ToList();
    }
}