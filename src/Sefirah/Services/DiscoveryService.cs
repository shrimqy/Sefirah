using System.Net;
using System.Text;
using CommunityToolkit.WinUI;
using MeaMod.DNS.Multicast;
using Microsoft.UI.Dispatching;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Contracts;
using Sefirah.Data.EventArguments;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Services.Socket;
using Sefirah.Utils;
using Sefirah.Utils.Serialization;

namespace Sefirah.Services;
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
    private readonly int port = 5149;
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = [];
    public List<DiscoveredMdnsServiceArgs> DiscoveredMdnsServices { get; } = [];
    private List<IPEndPoint> broadcastEndpoints = [];
    private const int DiscoveryPort = 5149;
    private readonly Lock collectionLock = new();

    public async Task StartDiscoveryAsync(int serverPort)
    {
        try
        {
            localDevice = await deviceManager.GetLocalDeviceAsync();

            var networkInterfaces = NetworkHelper.GetAllValidAddresses();

            var publicKey = Convert.ToBase64String(localDevice.PublicKey);
            var localAddresses = NetworkHelper.GetAllValidAddresses();

            logger.LogInformation($"Address to advertise: {string.Join(", ", localAddresses)}");

            var (name, avatar) = await UserInformation.GetCurrentUserInfoAsync();
            var udpBroadcast = new UdpBroadcast
            {
                DeviceId = localDevice.DeviceId,
                IpAddresses = [.. networkInterfaces.Select(i => i.Address.ToString())],
                Port = serverPort,
                DeviceName = name,
                PublicKey = publicKey,
            };

            mdnsService.AdvertiseService(udpBroadcast, port);

            broadcastEndpoints = [.. networkInterfaces.Select(ipInfo =>
            {
                var network = new Data.Models.IPNetwork(ipInfo.Address, ipInfo.SubnetMask);
                var broadcastAddress = network.BroadcastAddress;

                // Fallback to gateway if broadcast is limited
                return broadcastAddress.Equals(IPAddress.Broadcast) && ipInfo.Gateway != null
                    ? new IPEndPoint(ipInfo.Gateway, DiscoveryPort)
                    : new IPEndPoint(broadcastAddress, DiscoveryPort);

            }).Distinct()];

            // Always include default broadcast as fallback
            broadcastEndpoints.Add(new IPEndPoint(IPAddress.Parse(DEFAULT_BROADCAST), DiscoveryPort));

            var ipAddresses = deviceManager.GetRemoteDeviceIpAddresses();
            broadcastEndpoints.AddRange(ipAddresses.Select(ip => new IPEndPoint(IPAddress.Parse(ip), DiscoveryPort)));

            logger.LogInformation("Active broadcast endpoints: {endpoints}", string.Join(", ", broadcastEndpoints));


            udpClient = new MulticastClient("0.0.0.0", port, this, logger)
            {
                OptionDualMode = false,
                OptionMulticast = true,
                OptionReuseAddress = true,
            };
            udpClient.SetupMulticast(true);

            if (udpClient.Connect())
            {
                udpClient.Socket.EnableBroadcast = true;
                logger.LogInformation("UDP Client connected successfully {port}", port);

                BroadcastDeviceInfoAsync(udpBroadcast);
            }
            else
            {
                logger.LogError("Failed to connect UDP client");
            }

            mdnsService.DiscoveredMdnsService += OnDiscoveredMdnsService;
            mdnsService.ServiceInstanceShutdown += OnServiceInstanceShutdown;

        }
        catch (Exception ex)
        {
            logger.LogError("Discovery initialization failed: {message}", ex.Message);
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
                catch
                {
                    // ignore
                }
            }

            await Task.Delay(1000);
        }
    }

    private async void OnDiscoveredMdnsService(object? sender, DiscoveredMdnsServiceArgs service)
    {
        lock (collectionLock)
        {
            if (DiscoveredMdnsServices.Any(s => s.DeviceId == service.DeviceId)) return;

            DiscoveredMdnsServices.Add(service);
        }
        
        logger.LogInformation("Discovered service instance: {deviceId}, {deviceName}", service.DeviceId, service.DeviceName);

        var sharedSecret = EcdhHelper.DeriveKey(service.PublicKey, localDevice!.PrivateKey);
        DiscoveredDevice device = new(
            service.DeviceId,
            service.PublicKey,
            service.DeviceName,
            sharedSecret,
            DateTimeOffset.UtcNow,
            DeviceOrigin.MdnsService);

        await dispatcher.EnqueueAsync(() =>
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

    private async void OnServiceInstanceShutdown(object? sender, ServiceInstanceShutdownEventArgs e)
    {
        var deviceId = e.ServiceInstanceName.ToString().Split('.')[0];

        await dispatcher.EnqueueAsync(() =>
        {
            lock (collectionLock)
            {
                // Remove from MDNS services list
                DiscoveredMdnsServices.RemoveAll(s => s.DeviceId == deviceId);
                
                // Remove from discovered devices
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
                    logger.LogWarning("Device removal race condition: {Message}", ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Unexpected error removing device: {Message}", ex.Message);
                }
            }
        });
    }

    public async void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
    {
        try
        {
            var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            if (SocketMessageSerializer.DeserializeMessage(message) is not UdpBroadcast broadcast) return;

            if (broadcast.DeviceId == localDevice?.DeviceId) return;

            IPEndPoint? deviceEndpoint = broadcast.IpAddresses.Select(ip => new IPEndPoint(IPAddress.Parse(ip), DiscoveryPort)).FirstOrDefault();

            if (deviceEndpoint != null && !broadcastEndpoints.Contains(deviceEndpoint))
            {
                broadcastEndpoints.Add(deviceEndpoint);
            }

            // Skip if we already have this device via mDNS
            if (DiscoveredMdnsServices.Any(s => s.PublicKey == broadcast.PublicKey)) return;

            var sharedSecret = EcdhHelper.DeriveKey(broadcast.PublicKey, localDevice!.PrivateKey);
            DiscoveredDevice device = new(
                broadcast.DeviceId,
                broadcast.PublicKey,
                broadcast.DeviceName,
                sharedSecret,
                DateTimeOffset.FromUnixTimeMilliseconds(broadcast.TimeStamp),
                DeviceOrigin.UdpBroadcast);

            await dispatcher.EnqueueAsync(() =>
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

            StartCleanupTimer();
        }
        catch (Exception ex)
        {
            logger.LogWarning("Error processing UDP message: {message}", ex.Message);
        }
    }

    private DispatcherQueueTimer? _cleanupTimer;
    private readonly Lock _timerLock = new();
    
    private void StartCleanupTimer()
    {
        lock (_timerLock)
        {
            if (_cleanupTimer != null) return;

            _cleanupTimer = dispatcher.CreateTimer();
            _cleanupTimer.Interval = TimeSpan.FromSeconds(3);
            _cleanupTimer.Tick += (s, e) => CleanupStaleDevices();
            _cleanupTimer.Start();
        }
    }

    private void StopCleanupTimer()
    {
        lock (_timerLock)
        {
            _cleanupTimer?.Stop();
            _cleanupTimer = null;
        }
    }

    private async void CleanupStaleDevices()
    {
        try
        {
            await dispatcher.EnqueueAsync(() =>
            {
                lock (collectionLock)
                {
                    var now = DateTimeOffset.UtcNow;
                    var staleThreshold = TimeSpan.FromSeconds(5);

                    var staleDevices = DiscoveredDevices
                        .Where(d => d.Origin == DeviceOrigin.UdpBroadcast &&
                                  now - d.LastSeen > staleThreshold)
                        .ToList();

                    foreach (var device in staleDevices)
                    {
                            DiscoveredDevices.Remove(device);
                    }

                    // Stop timer if no UDP devices left
                    if (!DiscoveredDevices.Any(d => d.Origin == DeviceOrigin.UdpBroadcast))
                    {
                        StopCleanupTimer();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during cleanup of stale devices");
        }
    }

    public void Dispose()
    {
        StopCleanupTimer();

        try
        {
            udpClient?.Dispose();
            udpClient = null;
        }
        catch (Exception ex)
        {
            logger.LogError("Error disposing default UDP client: {message}", ex.Message);
        }
    }
}
