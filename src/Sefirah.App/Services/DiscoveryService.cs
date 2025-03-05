using CommunityToolkit.WinUI;
using MeaMod.DNS.Multicast;
using Microsoft.UI.Dispatching;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;
using Sefirah.App.Services.Socket;
using Sefirah.App.Utils;
using Sefirah.App.Utils.Serialization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using IPNetwork = Sefirah.App.Data.Models.IPNetwork;

namespace Sefirah.App.Services;
public class DiscoveryService(
    ILogger logger, 
    IMdnsService mdnsService, 
    IDeviceManager deviceManager
    ) : IDiscoveryService, IUdpClientProvider
{
    private MulticastClient? udpClient; // Default client for broadcasting
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
    private const string DEFAULT_BROADCAST = "255.255.255.255";
    private LocalDeviceEntity? localDevice;
    private readonly int port = 8689;
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = [];
    public List<DiscoveredMdnsServiceArgs> DiscoveredMdnsServices { get; } = [];
    private List<IPEndPoint> broadcastEndpoints = [];
    private const int DiscoveryPort = 8689;
    private readonly object collectionLock = new();

    public async Task StartDiscoveryAsync(int serverPort)
    {
        try
        {
            localDevice = await deviceManager.GetLocalDeviceAsync();
            var remoteDevice = await deviceManager.GetLastConnectedDevice();

            var networkInterfaces = NetworkHelper.GetAllValidAddresses();


            var publicKey = Convert.ToBase64String(localDevice.PublicKey);
            var localAddresses = NetworkHelper.GetAllValidAddresses();

            logger.Info($"Address to advertise: {string.Join(", ", localAddresses)}");

            var (username, avatar) = await CurrentUserInformation.GetCurrentUserInfoAsync();
            var udpBroadcast = new UdpBroadcast
            {
                DeviceId = localDevice.DeviceId,
                IpAddresses = [.. networkInterfaces.Select(i => i.Address.ToString())],
                Port = serverPort,
                DeviceName = username,
                PublicKey = publicKey,
            };

            mdnsService.AdvertiseService(udpBroadcast, port);

            broadcastEndpoints = networkInterfaces.Select(ipInfo => 
            {
                // Calculate proper broadcast address
                var network = new IPNetwork(ipInfo.Address, ipInfo.SubnetMask);
                var broadcastAddress = network.BroadcastAddress;
                
                // Fallback to gateway if broadcast is limited
                return broadcastAddress.Equals(IPAddress.Broadcast) && ipInfo.Gateway != null
                    ? new IPEndPoint(ipInfo.Gateway, DiscoveryPort)
                    : new IPEndPoint(broadcastAddress, DiscoveryPort);
                
            }).Distinct().ToList();

            // Always include default broadcast as fallback
            broadcastEndpoints.Add(new IPEndPoint(IPAddress.Parse(DEFAULT_BROADCAST), DiscoveryPort));
            
            if (remoteDevice != null && remoteDevice.IpAddresses != null)
            {
                logger.Info($"Remote device IP addresses: {string.Join(", ", remoteDevice.IpAddresses)}");
                broadcastEndpoints.AddRange(remoteDevice.IpAddresses.Select(ip => new IPEndPoint(IPAddress.Parse(ip), DiscoveryPort)));
            }

            logger.Info($"Active broadcast endpoints: {string.Join(", ", broadcastEndpoints)}");
           

            udpClient = new MulticastClient("0.0.0.0", port, this)
            {
                OptionDualMode = false,
                OptionMulticast = true,
                OptionReuseAddress = true,
            };
            udpClient.SetupMulticast(true);

            if (udpClient.Connect())
            {
                udpClient.Socket.EnableBroadcast = true;
                logger.Info("UDP Client connected successfully {0}", port);

                mdnsService.DiscoveredMdnsService += OnDiscoveredMdnsService;
                mdnsService.ServiceInstanceShutdown += OnServiceInstanceShutdown;

                BroadcastDeviceInfoAsync(udpBroadcast);
            }
            else
            {
                logger.Error("Failed to connect UDP client");
            }

        }
        catch (Exception ex)
        {
            logger.Error("Discovery initialization failed: {message}", ex.Message);
            throw;
        }
    }

    private async void BroadcastDeviceInfoAsync(UdpBroadcast udpBroadcast)
    {
        while (udpClient != null)
        {
            udpBroadcast.TimeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            string jsonMessage = SocketMessageSerializer.Serialize(udpBroadcast);
            byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
            foreach (var endPoint in broadcastEndpoints)
            {
                try
                {
                    udpClient.Socket.SendTo(messageBytes, endPoint);
                }
                catch (Exception ex)
                {
                    logger.Error("Error sending UDP broadcast: " + ex);
                }
            }

            await Task.Delay(1000);
        }
    }

    private void OnDiscoveredMdnsService(object? sender, DiscoveredMdnsServiceArgs service)
    {
        lock (collectionLock)
        {
            if (!DiscoveredMdnsServices.Any(s => s.DeviceId == service.DeviceId))
            {
                DiscoveredMdnsServices.Add(service);
                logger.Info("Discovered service instance: {0}, {1}", service.DeviceId, service.DeviceName);

                // Create device from mDNS data
                var sharedSecret = EcdhHelper.DeriveKey(service.PublicKey, localDevice!.PrivateKey);
                var device = new DiscoveredDevice
                {
                    DeviceId = service.DeviceId, // Assuming instance name is unique ID
                    DeviceName = service.DeviceName,
                    PublicKey = service.PublicKey,
                    HashedKey = sharedSecret,
                    LastSeen = DateTimeOffset.UtcNow,
                    Origin = DeviceOrigin.MdnsService
                };

                dispatcher.EnqueueAsync(() =>
                {
                    lock (collectionLock)
                    {
                        var existing = DiscoveredDevices.FirstOrDefault(d => d.DeviceId == device.DeviceId);
                        if (existing != null)
                        {
                            DiscoveredDevices[DiscoveredDevices.IndexOf(existing)] = device;
                        }
                        else
                        {
                            DiscoveredDevices.Add(device);
                        }
                    }
                });
            }
        }
    }

    private void OnServiceInstanceShutdown(object? sender, ServiceInstanceShutdownEventArgs e)
    {
        var deviceId = e.ServiceInstanceName.ToString().Split('.')[0];

        // Remove from MDNS services list first
        lock (collectionLock)
        {
            DiscoveredMdnsServices.RemoveAll(s => s.DeviceId == deviceId);
        }

        // Remove corresponding device from main collection
        dispatcher.EnqueueAsync(() =>
        {
            lock (collectionLock)
            {
                try
                {
                    var deviceToRemove = DiscoveredDevices
                        .Where(d => d.Origin == DeviceOrigin.MdnsService)
                        .FirstOrDefault(d => d.DeviceId == deviceId);
                    
                    if (deviceToRemove != null)
                    {
                        DiscoveredDevices.Remove(deviceToRemove);
                    }
                }
                catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
                {
                    logger.Warn("Device removal race condition: {Message}", ex.Message);
                }
                catch (Exception ex)
                {
                    logger.Error("Unexpected error removing device: {Message}", ex.Message);
                }
            }
        });
    }

    public void OnConnected()
    {

    }

    public void OnDisconnected()
    {
        
    }

    public void OnError(SocketError error)  
    {
        
    }

    public void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
    {
        try
        {
            var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            if (SocketMessageSerializer.DeserializeMessage(message) is not UdpBroadcast broadcast) return;

            // Skip if we already have this device via mDNS
            if (DiscoveredMdnsServices.Any(s => s.PublicKey == broadcast.PublicKey)) 
            {
                return;
            }

            // Ignore our own broadcasts
            if (broadcast.DeviceId == localDevice?.DeviceId) return;

            var sharedSecret = EcdhHelper.DeriveKey(broadcast.PublicKey, localDevice!.PrivateKey);
            var device = new DiscoveredDevice
            {
                DeviceId = broadcast.DeviceId,
                DeviceName = broadcast.DeviceName,
                PublicKey = broadcast.PublicKey,
                HashedKey = sharedSecret,
                LastSeen = DateTimeOffset.FromUnixTimeMilliseconds(broadcast.TimeStamp),
                Origin = DeviceOrigin.UdpBroadcast
            };

            // Update or add device to collection
            dispatcher.EnqueueAsync(() =>
            {
                lock (collectionLock)
                {
                    var existingDevice = DiscoveredDevices.FirstOrDefault(d => d.DeviceId == device.DeviceId);
                    if (existingDevice != null)
                    {
                        var index = DiscoveredDevices.IndexOf(existingDevice);
                        DiscoveredDevices[index] = device;
                    }
                    else
                    {
                        DiscoveredDevices.Add(device);
                    }
                }
            });

            // Start cleanup timer if not already started
            StartCleanupTimer();
        }
        catch (Exception ex)
        {
            logger.Error("Error processing UDP message: {message}", ex.Message);
        }
    }

    private DispatcherQueueTimer? _cleanupTimer;
    private void StartCleanupTimer()
    {
        if (_cleanupTimer != null) return;

        _cleanupTimer = dispatcher.CreateTimer();
        _cleanupTimer.Interval = TimeSpan.FromSeconds(3);
        _cleanupTimer.Tick += (s, e) => CleanupStaleDevices();
        _cleanupTimer.Start();
    }

    private void CleanupStaleDevices()
    {
        var now = DateTimeOffset.UtcNow;
        var staleThreshold = TimeSpan.FromSeconds(5);

        var staleDevices = DiscoveredDevices
            .Where(d => d.Origin == DeviceOrigin.UdpBroadcast && 
                      now - d.LastSeen > staleThreshold)
            .ToList();

        foreach (var device in staleDevices)
        {
            lock (collectionLock)
            {
                dispatcher.EnqueueAsync(() =>
                {
                    DiscoveredDevices.Remove(device);
                });
            }
        }

        // Stop timer if no UDP devices left
        if (DiscoveredDevices.All(d => d.Origin != DeviceOrigin.UdpBroadcast))
        {
            _cleanupTimer?.Stop();
            _cleanupTimer = null;
        }
    }

    public void Dispose()
    {
        // Dispose default client
        try
        {
            udpClient?.Dispose();
            udpClient = null;
        }
        catch (Exception ex)
        {
            logger.Error("Error disposing default UDP client: {message}", ex.Message);
        }
    }
}
