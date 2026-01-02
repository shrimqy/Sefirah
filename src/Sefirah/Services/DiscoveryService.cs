using System.Net;
using System.Text;
using Microsoft.UI.Xaml.Media.Imaging;
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
                PublicKey = Convert.ToBase64String(localDevice.PublicKey),
                Port = NetworkService.ServerPort
            };

            mdnsService.AdvertiseService(BroadcastMessage, port);
            mdnsService.StartDiscovery();
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
                BroadcastDeviceInfoAsync(BroadcastMessage);
            }
            else
            {
                logger.LogError("Failed to connect UDP client");
            }

            mdnsService.DiscoveredMdnsService += OnDiscoveredMdnsService;

        }
        catch (Exception ex)
        {
            logger.LogError("Discovery initialization failed: {message}", ex.Message);
        }
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
                logger.LogInformation("Sending UDP message to {endpoint}", endPoint);
                udpClient.Socket.SendTo(messageBytes, endPoint);
            }
            catch
            {
                // ignore
            }
        }
        
    }

    private async void OnDiscoveredMdnsService(object? sender, DiscoveredMdnsServiceArgs service)
    {        
        await sessionManager.ConnectTo(service.DeviceId, service.Address, service.Port, service.PublicKey);
    }

    public async void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
    {
        try
        {
            var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

            var address = ((IPEndPoint)endpoint).Address;
            if (JsonMessageSerializer.DeserializeMessage(message) is not UdpBroadcast broadcast) return;

            if (broadcast.DeviceId == localDevice?.DeviceId || address is null) return;

            await sessionManager.ConnectTo(broadcast.DeviceId, address.ToString(), broadcast.Port, broadcast.PublicKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Error processing UDP message: {message}", ex.Message);
        }
    }

    public void StopDiscovery()
    {
        try
        {
            udpClient?.Dispose();
            udpClient = null;
            mdnsService.UnAdvertiseService();
        }
        catch (Exception ex)
        {
            logger.LogError("Error disposing default UDP client: {message}", ex.Message);
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

            var connectionInfo = new
            {
                Addresses = addresses,
                broadcast.Port,
                broadcast.DeviceId,
                broadcast.DeviceName,
                broadcast.PublicKey
            };

            var jsonData = JsonMessageSerializer.Serialize(connectionInfo);

            var qrCodeBytes = ImageHelper.GenerateQrCode(jsonData);
            if (qrCodeBytes is null)
            {
                return null;
            }

            return await qrCodeBytes.ToBitmapAsync(256);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Error generating QR code: {message}", ex.Message);
            return null;
        }
    }

}
