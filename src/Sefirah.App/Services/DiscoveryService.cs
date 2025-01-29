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
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Sefirah.App.Services;
public class DiscoveryService(
    ILogger logger, 
    IMdnsService mdnsService, 
    IDeviceManager deviceManager
    ) : IDiscoveryService, IUdpClientProvider
{
    private readonly Dictionary<int, MulticastClient> udpClients = [];
    private MulticastClient? udpClient; // Default client for broadcasting
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
    private const string MULTICAST_ADDRESS = "255.255.255.255";
    private LocalDeviceEntity? localDevice;
    private int port;
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = [];
    public List<DiscoveredMdnsServiceArgs> DiscoveredMdnsServices { get; } = [];

    public async Task StartDiscoveryAsync(int serverPort)
    {
        port = await NetworkHelper.FindAvailablePortAsync(8689);
        mdnsService.AdvertiseService(port);
        mdnsService.DiscoveredMdnsService += OnDiscoveredMdnsService;
        mdnsService.ServiceInstanceShutdown += OnServiceInstanceShutdown;
        try
        {
            localDevice = await deviceManager.GetLocalDeviceAsync();
            var publicKey = Convert.ToBase64String(localDevice.PublicKey);
            var localAddresses = NetworkHelper.GetAllValidAddresses();

            logger.Info($"Address to advertise: {string.Join(", ", localAddresses)}");

            udpClient = new MulticastClient("0.0.0.0", port, this)
            {
                OptionDualMode = true,
                OptionMulticast = true,
                OptionReuseAddress = true,
            };
            udpClient.SetupMulticast(true);

            if (udpClient.Connect())
            {
                udpClient.Socket.EnableBroadcast = true;
                logger.Info("UDP Client connected successfully {0}", port);
                var (username, avatar) = await CurrentUserInformation.GetCurrentUserInfoAsync();
                var udpBroadcast = new UdpBroadcast
                {
                    DeviceId = localDevice.DeviceId,
                    IpAddresses = [.. localAddresses],
                    Port = serverPort,
                    DeviceName = username,
                    PublicKey = publicKey,
                };

                BroadcastDeviceInfoAsync(udpBroadcast);
            }
            else
            {
                logger.Error("Failed to connect UDP client");
            }

        }
        catch (Exception ex)
        {
            logger.Error("Error in UDP broadcast: {message}", ex.Message);
            throw;
        }
    }

    private async void BroadcastDeviceInfoAsync(UdpBroadcast udpBroadcast)
    {

        var broadcastEndpoint = new IPEndPoint(IPAddress.Parse(MULTICAST_ADDRESS), port);
        while (udpClient != null)
        {
            udpBroadcast.TimeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            string jsonMessage = SocketMessageSerializer.Serialize(udpBroadcast);
            byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
            udpClient.Socket.SendTo(messageBytes, broadcastEndpoint);
            await Task.Delay(1000);
        }
    }

    private void OnDiscoveredMdnsService(object? sender, DiscoveredMdnsServiceArgs service)
    {
        if (!DiscoveredMdnsServices.Any(s => s.ServiceInstanceName == service.ServiceInstanceName))
        {
            DiscoveredMdnsServices.Add(service);
            logger.Info("Discovered service instance: {0}, {1}", service.ServiceInstanceName, service.Port);
            
            // Don't create duplicate listener if it's our broadcasting port
            if (udpClient?.Socket.LocalEndPoint is IPEndPoint localEndPoint && 
                localEndPoint.Port == service.Port)
            {
                logger.Debug("Port {0} is already used by default UDP client", service.Port);
                return;
            }

            // Check if we already have a listener for this port
            if (!udpClients.ContainsKey(service.Port))
            {
                try
                {
                    var client = new MulticastClient("0.0.0.0", service.Port, this)
                    {
                        OptionDualMode = true,
                        OptionMulticast = true,
                        OptionReuseAddress = true,
                    };
                    client.SetupMulticast(true);

                    if (client.Connect())
                    {
                        client.Socket.EnableBroadcast = true;
                        udpClients.Add(service.Port, client);
                        logger.Info("Started UDP listener on port {0}", service.Port);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Failed to create UDP listener on port {0}: {1}", service.Port, ex.Message);
                }
            }
        }
    }

    private void OnServiceInstanceShutdown(object? sender, ServiceInstanceShutdownEventArgs e)
    {
        DiscoveredMdnsServices.RemoveAll(s => s.ServiceInstanceName == e.ServiceInstanceName);
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

            // Ignore our own broadcasts
            if (broadcast.DeviceId == localDevice?.DeviceId) return;

            var sharedSecret = EcdhHelper.DeriveKey(broadcast.PublicKey, localDevice!.PrivateKey);
            var device = new DiscoveredDevice
            {
                DeviceId = broadcast.DeviceId,
                DeviceName = broadcast.DeviceName,
                PublicKey = broadcast.PublicKey,
                HashedKey = sharedSecret,
                LastSeen = DateTimeOffset.FromUnixTimeMilliseconds(broadcast.TimeStamp)
            };

            // Update or add device to collection
            dispatcher.EnqueueAsync(() =>
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
            .Where(d => now - d.LastSeen > staleThreshold)
            .ToList();

        foreach (var device in staleDevices)
        {
            DiscoveredDevices.Remove(device);
        }

        // Stop timer if no devices left
        if (DiscoveredDevices.Count == 0)
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

        // Dispose additional clients
        foreach (var client in udpClients.Values)
        {
            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error("Error disposing UDP client: {message}", ex.Message);
            }
        }
        udpClients.Clear();
    }
}
