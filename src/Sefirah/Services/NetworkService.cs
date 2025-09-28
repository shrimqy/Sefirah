using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CommunityToolkit.WinUI;
using NetCoreServer;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Services.Socket;
using Sefirah.Utils;
using Sefirah.Utils.Serialization;
using Uno.Logging;

namespace Sefirah.Services;
public class NetworkService(
    Func<IMessageHandler> messageHandlerFactory,
    ILogger<NetworkService> logger,
    IDeviceManager deviceManager,
    IAdbService adbService,
    IDiscoveryService discoveryService) : INetworkService, ISessionManager, ITcpServerProvider
{
    private Server? server;
    private int serverPort;
    private bool isRunning;
    private X509Certificate2? certificate;
    private readonly IEnumerable<int> PORT_RANGE = Enumerable.Range(5150, 20); // 5150 to 5169

    private readonly Lazy<IMessageHandler> messageHandler = new(messageHandlerFactory);

    private string bufferedData = string.Empty;
    
    private ObservableCollection<PairedDevice> PairedDevices => deviceManager.PairedDevices;

    /// <summary>
    /// Event fired when a device connection status changes
    /// </summary>
    public event EventHandler<(PairedDevice Device, bool IsConnected)>? ConnectionStatusChanged;

    public async Task<bool> StartServerAsync()
    {
        if (isRunning)
        {
            logger.LogWarning("Server is already running");
            return false;
        }
        try
        {

            certificate = await CertificateHelper.GetOrCreateCertificateAsync();

            var context = new SslContext(SslProtocols.Tls12 | SslProtocols.Tls13, certificate, (sender, cert, chain, errors) => true);

            foreach (int port in PORT_RANGE)
            {
                try
                {
                    server = new Server(context, IPAddress.Any, port, this, logger)
                    {
                        OptionReuseAddress = true,
                    };

                    if (server.Start())
                    {
                        serverPort = port;
                        isRunning = true;
                        await discoveryService.StartDiscoveryAsync(port);
                        logger.Info($"Server started on port: {port}");
                        return true;
                    }
                    else
                    {
                        server.Dispose();
                        server = null;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Error starting server on port {port}", ex);
                    server?.Dispose();
                    server = null;
                }
            }

            logger.LogError("Failed to start server");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError("Error starting server {ex}", ex);
            return false;
        }
    }

    public void SendMessage(ServerSession session, string message)
    {
        try
        {
            string messageWithNewline = message + "\n";
            byte[] messageBytes = Encoding.UTF8.GetBytes(messageWithNewline);

            session.Send(messageBytes, 0, messageBytes.Length);
        }
        catch (Exception ex)
        {
            logger.LogError("Error sending message {ex}", ex);
        }
    }

    public void BroadcastMessage(string message)
    {
        if (PairedDevices.Count == 0) return;
        try
        {
            foreach (var device in PairedDevices.Where(d => d.Session != null))
            {
                SendMessage(device.Session!, message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Error sending message to all {ex}", ex);
        }
    }

    // Server side methods
    public void OnConnected(ServerSession session)
    {

    }

    public void OnDisconnected(ServerSession session)
    {
        DisconnectSession(session);
    }

    public void OnError(SocketError error)
    {
        logger.LogError("Error on socket {error}", error);
    }

    public async void OnReceived(ServerSession session, byte[] buffer, long offset, long size)
    {
        try
        {
            string newData = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            //logger.Debug($"Received raw data: {(newData.Length > 100 ? string.Concat(newData.AsSpan(0, Math.Min(100, newData.Length)), "...") : newData)}");

            bufferedData += newData;

            while (true)
            {
                int newlineIndex = bufferedData.IndexOf('\n');
                if (newlineIndex == -1)
                {
                    break;
                }

                string message = bufferedData[..newlineIndex].Trim();

                // Check if newlineIndex is at the end of the string
                if (newlineIndex + 1 >= bufferedData.Length)
                {
                    bufferedData = string.Empty;
                }
                else
                {
                    bufferedData = bufferedData[(newlineIndex + 1)..];
                }

                if (string.IsNullOrEmpty(message)) continue;
        
                var device = PairedDevices.FirstOrDefault(d => d.Session?.Id == session.Id);
                if (device != null)
                {
                    ProcessMessage(device, message);
                }
                else
                {
                    await HandleVerification(session, message);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Error in OnReceived for session {id}: {ex}", session.Id, ex);
            DisconnectSession(session);
        }
    }

    private async Task HandleVerification(ServerSession session, string message)
    {
        try
        {
            if (SocketMessageSerializer.DeserializeMessage(message) is not DeviceInfo deviceInfo)
            {
                logger.Warn("Invalid device info or first message wasn't deviceInfo");
                DisconnectSession(session);
                return;
            }


            if (string.IsNullOrEmpty(deviceInfo.Nonce) || string.IsNullOrEmpty(deviceInfo.Proof))
            {
                logger.Warn("Missing authentication data");
                DisconnectSession(session);
                return;
            }

            var connectedSessionIpAddress = session.Socket.RemoteEndPoint?.ToString()?.Split(':')[0];
            logger.Info($"Received connection from {connectedSessionIpAddress}");

            var device = await deviceManager.VerifyDevice(deviceInfo, connectedSessionIpAddress);

            if (device != null)
            {
                logger.Info($"Device {device.Id} connected");
                
                deviceManager.UpdateOrAddDevice(device, connectedDevice  =>
                {

                    connectedDevice.ConnectionStatus = true;
                    connectedDevice.Session = session;
                    
                    deviceManager.ActiveDevice = connectedDevice;
                    device = connectedDevice;

                    if (device.DeviceSettings.AdbAutoConnect && connectedSessionIpAddress != null)
                    {
                        adbService.TryConnectTcp(connectedSessionIpAddress);
                    }
                });

                var (_, avatar) = await UserInformation.GetCurrentUserInfoAsync();
                var localDevice = await deviceManager.GetLocalDeviceAsync();

                // Generate our authentication proof
                var sharedSecret = EcdhHelper.DeriveKey(deviceInfo.PublicKey!, localDevice.PrivateKey);
                var nonce = EcdhHelper.GenerateNonce();
                var proof = EcdhHelper.GenerateProof(sharedSecret, nonce);

                SendMessage(session, SocketMessageSerializer.Serialize(new DeviceInfo
                {
                    DeviceId = localDevice.DeviceId,
                    DeviceName = localDevice.DeviceName,
                    Avatar = avatar,
                    PublicKey = Convert.ToBase64String(localDevice.PublicKey),
                    Nonce = nonce,
                    Proof = proof
                }));

                ConnectionStatusChanged?.Invoke(this, (device, true));
            }
            else
            {
                SendMessage(session, "Rejected");
                await Task.Delay(50);
                logger.Info("Device verification failed or was declined");
                DisconnectSession(session);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error processing first message for session {session.Id}: {ex}");
            DisconnectSession(session);
        }
    }

    private async void ProcessMessage(PairedDevice device, string message)
    {
        try
        {
            logger.Debug($"Processing message: {(message.Length > 100 ? string.Concat(message.AsSpan(0, Math.Min(100, message.Length)), "...") : message)}");

            // Check if this looks like a JSON message before attempting to deserialize
            if (message.TrimStart().StartsWith('{') || message.TrimStart().StartsWith('['))
            {
                var socketMessage = SocketMessageSerializer.DeserializeMessage(message);
                if (socketMessage != null)
                {
                    await messageHandler.Value.HandleMessageAsync(device, socketMessage);
                }
                return;
            }
            logger.Debug("Received non-JSON data, skipping JSON parsing");
        }
        catch (JsonException jsonEx)
        {
            logger.Error($"Error parsing JSON message: {jsonEx.Message}");
        }
    }

    public void DisconnectSession(ServerSession session)
    {
        try
        {
            bufferedData = string.Empty;
            session.Disconnect();
            session.Dispose();
            var device = PairedDevices.FirstOrDefault(d => d.Session == session);   
            if (device != null)
            {
                App.MainWindow?.DispatcherQueue.EnqueueAsync(() =>
                {
                    device.ConnectionStatus = false;
                    device.Session = null;
                    logger.Info($"Device {device.Name} session disconnected, status updated");
                });
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error in Disconnecting: {ex.Message}");
        }
    }
}
