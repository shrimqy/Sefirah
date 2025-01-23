using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;

namespace Sefirah.App.Utils;

public static class NetworkHelper
{
    public static string GetLocalIPAddress(bool preferIPv6 = true)
    {
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if ((ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || 
                 ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                ni.OperationalStatus == OperationalStatus.Up &&
                !IsVirtualAdapter(ni))
            {
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    // For IPv6, include the scope ID for link-local addresses
                    if (preferIPv6 && 
                        ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        // Include scope ID for link-local addresses
                        string ipString = ip.Address.ToString();
                        if (ip.Address.IsIPv6LinkLocal)
                        {
                            ipString = $"{ipString}%{ip.Address.ScopeId}";
                        }
                        return ipString;
                    }
                    // IPv4 fallback
                    else if (ip.Address.AddressFamily == AddressFamily.InterNetwork && 
                            !IPAddress.IsLoopback(ip.Address))
                    {
                        return ip.Address.ToString();
                    }
                }
            }
        }

        throw new Exception("No network adapters with a valid IP address found!");
    }

    private static bool IsVirtualAdapter(NetworkInterface ni)
    {
        // Filter out adapters with "vEthernet" or other virtual identifiers in their name
        return ni.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase) ||
               ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
               ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
               ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase);
    }

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

        var error = "No available ports found";
        throw new InvalidOperationException(error);
    }

    public static string GetLocalIPv6Address()
    {
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if ((ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || 
                 ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                ni.OperationalStatus == OperationalStatus.Up &&
                !IsVirtualAdapter(ni))
            {
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        // Prefer non-link-local addresses
                        if (!ip.Address.IsIPv6LinkLocal)
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
                
                // If no non-link-local address found, fall back to link-local
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
                        ip.Address.IsIPv6LinkLocal)
                    {
                        return $"{ip.Address}%{ip.Address.ScopeId}";
                    }
                }
            }
        }

        throw new Exception("No network adapters with a valid IPv6 address found!");
    }

    public static (string Address, AddressType Type) GetBestAddress()
    {
        var addresses = new List<(string Address, AddressType Type, int Priority)>();
        
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if ((ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || 
                 ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                ni.OperationalStatus == OperationalStatus.Up &&
                !IsVirtualAdapter(ni))
            {
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    // Skip loopback addresses
                    if (IPAddress.IsLoopback(ip.Address)) continue;
                    
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        // Regular IPv4
                        if (IsPrivateNetwork(ip.Address))
                        {
                            addresses.Add((ip.Address.ToString(), AddressType.IPv4Private, 1));
                        }
                        else
                        {
                            addresses.Add((ip.Address.ToString(), AddressType.IPv4Public, 2));
                        }
                    }
                    else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        if (ip.Address.IsIPv6LinkLocal)
                        {
                            // Lowest priority - link local only works on same network segment
                            addresses.Add(($"{ip.Address}%{ip.Address.ScopeId}", 
                                        AddressType.IPv6LinkLocal, 0));
                        }
                        else if (IsIPv6UniqueLocal(ip.Address))
                        {
                            // ULA addresses (fc00::/7) - like private IPv4
                            addresses.Add((ip.Address.ToString(), 
                                        AddressType.IPv6UniqueLocal, 3));
                        }
                        else
                        {
                            // Global IPv6 - highest priority
                            addresses.Add((ip.Address.ToString(), 
                                        AddressType.IPv6Global, 4));
                        }
                    }
                }
            }
        }

        // Get highest priority available address
        var bestAddress = addresses
            .OrderByDescending(a => a.Priority)
            .FirstOrDefault();
            
        return bestAddress.Address != null 
            ? (bestAddress.Address, bestAddress.Type)
            : throw new Exception("No suitable network address found");
    }

    public enum AddressType
    {
        IPv4Private,
        IPv4Public,
        IPv6LinkLocal,
        IPv6UniqueLocal,
        IPv6Global
    }

    private static bool IsPrivateNetwork(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return
            bytes[0] == 10 || // 10.0.0.0/8
            (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // 172.16.0.0/12
            (bytes[0] == 192 && bytes[1] == 168); // 192.168.0.0/16
    }

    private static bool IsIPv6UniqueLocal(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return (bytes[0] & 0xFE) == 0xFC; // fc00::/7
    }
}