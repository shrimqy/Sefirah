using System.Net;
using System.Text;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.AppDatabase.Models;
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
    IDeviceManager deviceManager,
    ISessionManager sessionManager
    ) : IDiscoveryService, IUdpClientProvider
{
    private MulticastClient? udpClient; 
    private const string DEFAULT_BROADCAST = "255.255.255.255";
    private LocalDeviceEntity? localDevice;
    private readonly int port = 5149;
    private List<IPEndPoint> broadcastEndpoints = [];
    private const int DiscoveryPort = 5149;

    public UdpBroadcast? BroadcastMessage { get; private set; }

    public async Task StartDiscoveryAsync()
    {
        try
        {
            localDevice = await deviceManager.GetLocalDeviceAsync();
            var localAddresses = NetworkHelper.GetAllValidAddresses();

            var name = await UserInformation.GetCurrentUserNameAsync();
            BroadcastMessage = new UdpBroadcast
            {
                DeviceId = localDevice.DeviceId,
                DeviceName = name,
                Port = NetworkService.ServerPort
            };

            mdnsService.AdvertiseService(BroadcastMessage, port);
            mdnsService.StartDiscovery();
            mdnsService.DiscoveredMdnsService += OnDiscoveredMdnsService;

            broadcastEndpoints = [.. localAddresses.Select(ipInfo =>
            {
                var network = new Data.Models.IPNetwork(ipInfo.Address, ipInfo.SubnetMask);
                var broadcastAddress = network.BroadcastAddress;

                // Fallback to gateway if broadcast is limited
                return broadcastAddress.Equals(IPAddress.Broadcast) && ipInfo.Gateway is not null
                    ? new IPEndPoint(ipInfo.Gateway, DiscoveryPort)
                    : new IPEndPoint(broadcastAddress, DiscoveryPort);

            }).Distinct()];

            // Always include default broadcast as fallback
            broadcastEndpoints.Add(new IPEndPoint(IPAddress.Parse(DEFAULT_BROADCAST), DiscoveryPort));

            var addresses = deviceManager.GetRemoteDeviceAddresses();
            broadcastEndpoints.AddRange(addresses.Select(address => new IPEndPoint(IPAddress.Parse(address), DiscoveryPort)));

            logger.Info($"Active broadcast endpoints: {string.Join(", ", broadcastEndpoints)}");

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
                logger.Info($"UDP Client connected successfully {port}");
                BroadcastDeviceInfoAsync(BroadcastMessage);
            }
            else
            {
                logger.Error("Failed to connect UDP client");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Discovery initialization failed: {ex.Message}", ex);
        }
    }

    private void OnDiscoveredMdnsService(object? sender, DiscoveredMdnsServiceArgs e)
    {
        sessionManager.Connect(e.DeviceId, e.Address, e.Port);
    }

    private async void BroadcastDeviceInfoAsync(UdpBroadcast udpBroadcast)
    {
        if (udpClient is null || udpBroadcast is null) return;
        
        string jsonMessage = JsonMessageSerializer.Serialize(udpBroadcast);
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
    }

    public async void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
    {
        try
        {
            var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            var address = ((IPEndPoint)endpoint).Address;
            if (JsonMessageSerializer.DeserializeMessage(message) is not UdpBroadcast broadcast) return;

            if (broadcast.DeviceId == localDevice?.DeviceId || address is null) return;

            sessionManager.Connect(broadcast.DeviceId, address.ToString(), broadcast.Port);
        }
        catch (Exception ex)
        {
            logger.Warn($"Error processing UDP message: {ex.Message}", ex);
        }
    }

    public void StopDiscovery()
    {
        try
        {
            mdnsService.DiscoveredMdnsService -= OnDiscoveredMdnsService;
            mdnsService.UnAdvertiseService();
            udpClient?.Dispose();
            udpClient = null;
        }
        catch (Exception ex)
        {
            logger.Error($"Error disposing default UDP client: {ex.Message}", ex);
        }
    }

    public async Task<BitmapImage?> GenerateQrCodeAsync()
    {
        try
        {
            var broadcast = BroadcastMessage;
            if (broadcast is null)
            {
                return null;
            }

            var localAddresses = NetworkHelper.GetAllValidAddresses();
            var addresses = localAddresses.Select(addr => addr.Address.ToString()).ToList();

            var payload = new QrCodePayload
            {
                Addresses = addresses,
                Port = broadcast.Port,
                DeviceId = broadcast.DeviceId,
                DeviceName = broadcast.DeviceName
            };
            var json = JsonMessageSerializer.Serialize(payload);
            var deepLink = $"sefirah://pair?data={Uri.EscapeDataString(json)}";

            var qrCodeBytes = ImageHelper.GenerateQrCode(deepLink);
            if (qrCodeBytes is null)
            {
                return null;
            }

            return await qrCodeBytes.ToBitmapAsync(256);
        }
        catch (Exception ex)
        {
            logger.Warn($"Error generating QR code: {ex.Message}", ex);
            return null;
        }
    }

}
