using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CommunityToolkit.WinUI;
using NetCoreServer;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Dialogs;
using Sefirah.Helpers;
using Sefirah.Services.Socket;
using Sefirah.Utils;
using Sefirah.Utils.Serialization;

namespace Sefirah.Services;

public class NetworkService(
    Lazy<IMessageHandler> messageHandler,
    ILogger<NetworkService> logger,
    IDeviceManager deviceManager,
    IAdbService adbService) : INetworkService, ISessionManager, ITcpServerProvider, ITcpClientProvider
{
    public static int ServerPort { get; private set; }

    private Server? server;
    private bool isRunning;
    private static SslContext SslContext => CertificateHelper.SslContext;
    private static readonly IEnumerable<int> PORT_RANGE = Enumerable.Range(5150, 20); // 5150 to 5169

    private readonly ConcurrentDictionary<Guid, string> connectionBuffers = [];
    private readonly HashSet<string> connectingDeviceIds = [];
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> handshakeCompletion = [];
    
    private ObservableCollection<PairedDevice> PairedDevices => deviceManager.PairedDevices;
    private ObservableCollection<DiscoveredDevice> DiscoveredDevices => deviceManager.DiscoveredDevices;

    /// <summary>
    /// Event fired when a device connection status changes
    /// </summary>
    public event EventHandler<PairedDevice>? ConnectionStatusChanged;

    public async Task StartServerAsync()
    {
        if (isRunning) return;

        try
        {
            ConnectionStatusChanged += ConnectionStatusChangedEvent;

            foreach (int port in PORT_RANGE)
            {
                try
                {
                    server = new Server(SslContext, IPAddress.Any, port, this, logger)
                    {
                        OptionReuseAddress = true,
                    };

                    if (server.Start())
                    {
                        ServerPort = port;
                        isRunning = true;
                        logger.Info($"Server started on port: {port}");
                        return;
                    }
                    server.Dispose();
                    server = null;
                }
                catch (Exception ex)
                {
                    logger.Error($"Error starting server on port {port}", ex);
                    server?.Dispose();
                    server = null;
                }
            }
            logger.LogError("Failed to start server");
        }
        catch (Exception ex)
        {
            logger.LogError("Error starting server {ex}", ex);
        }
    }

    private async void ConnectionStatusChangedEvent(object? sender, PairedDevice device)
    {
        if (device.IsConnected)
        {
            await SendDeviceInfo(device);
            await adbService.TryConnectTcp(device.Address, device.Model);
        }
    }

    private async Task SendDeviceInfo(PairedDevice device)
    {
        try
        {
            logger.Info("Sending deviceInfo");
            var localDevice = await deviceManager.GetLocalDeviceAsync();
            var avatar = await UserInformation.GetCurrentUserAvatarAsync();
            device.SendMessage(new DeviceInfo { DeviceName = localDevice.DeviceName, Avatar = avatar });
        }
        catch (Exception ex)
        {
            logger.Error("Excetpion occured while sending device info", ex);
        }
    }

    public void DisconnectDevice(PairedDevice device, bool forcedDisconnect = false)
    {
        if (device.Session is not null)
        {
            DisconnectSession(device.Session, true);
        }
        else if (device.Client is not null)
        {
            DisconnectClient(device.Client, true);
        }
    }

    public void SendMessage(ServerSession session, SocketMessage message)
    {
        try
        {
            var bytes = EncodeMessage(message);
            session.SendAsync(bytes);
        }
        catch (Exception ex)
        {
            logger.LogError("Error sending message {ex}", ex);
        }
    }

    public void SendMessage(Client client, SocketMessage message)
    {
        try
        {
            var bytes = EncodeMessage(message);
            client.SendAsync(bytes);
        }
        catch (Exception ex)
        {
            logger.LogError("Error sending message to client {ex}", ex);
        }
    }

    private static byte[] EncodeMessage(SocketMessage message) =>
        Encoding.UTF8.GetBytes(JsonMessageSerializer.Serialize(message) + "\n");

    public void BroadcastMessage(SocketMessage message)
    {
        if (PairedDevices.Count == 0) return;
        try
        {
            foreach (var device in PairedDevices.Where(d => d.IsConnected))
            {
                device.SendMessage(message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Error sending message to all {ex}", ex);
        }
    }

    #region Server events
    public void OnConnected(ServerSession session) 
    {
        connectionBuffers[session.Id] = string.Empty;
    }

    public void OnDisconnected(ServerSession session)
    {
        if (!connectionBuffers.ContainsKey(session.Id)) return;

        logger.Debug($"Session disconnected");
        DisconnectSession(session);
    }

    public void OnError(SocketError error)
    {
        logger.LogError("Error on socket {error}", error);
    }

    public void OnReceived(ServerSession session, byte[] buffer, long offset, long size)
    {
        try
        {
            string newData = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            if (!connectionBuffers.TryGetValue(session.Id, out var buffered))
            {
                buffered = string.Empty;
            }

            buffered += newData;
            
            while (buffered.IndexOf('\n') is var newlineIndex && newlineIndex != -1)
            {
                string messageString = buffered[..newlineIndex].Trim();
                buffered = newlineIndex + 1 >= buffered.Length ? string.Empty : buffered[(newlineIndex + 1)..];

                if (string.IsNullOrEmpty(messageString)) continue;

                logger.Debug($"Processing message: {(messageString.Length > 100 ? string.Concat(messageString.AsSpan(0, Math.Min(100, messageString.Length)), "...") : messageString)}");

                var socketMessage = JsonMessageSerializer.DeserializeMessage(messageString);
                if (socketMessage is null) continue;

                logger.Debug($"Received message: {socketMessage.GetType().Name}");

                if (socketMessage is AuthenticationMessage authMessage)
                {
                    HandleServerSessionAuthentication(session, authMessage);
                    return;
                }

                RouteMessage(session.Id, socketMessage);
            }

            connectionBuffers[session.Id] = buffered;
        }
        catch (Exception ex)
        {
            logger.LogError("Error in OnReceived for session {id}: {ex}", session.Id, ex);
        }
    }



    /// <summary>
    /// Routes a deserialized message from a client and server to the appropriate handler
    /// </summary>
    private async void RouteMessage(Guid guid, SocketMessage message)
    {
        try
        {
            // Check if this is from a connected paired device
            var pairedDevice = PairedDevices.FirstOrDefault(d => (d.Client?.Id == guid || d.Session?.Id == guid) && d.IsConnected);
            if (pairedDevice is not null)
            {
                messageHandler.Value.HandleMessageAsync(pairedDevice, message);
                return;
            }

            var discoveredDevice = DiscoveredDevices.FirstOrDefault(d => d.Client?.Id == guid || d.Session?.Id == guid);
            if (discoveredDevice is not null && message is PairMessage pairMessage)
            {
                HandlePairMessage(discoveredDevice, pairMessage);
            }

            logger.Warn("Received message from unknown client");
        }
        catch (Exception ex)
        {
            logger.Error($"Error routing client message: {ex}");
        }
    }

    private async void HandlePairMessage(DiscoveredDevice device, PairMessage pairMessage)
    {
        if (device.IsPairing)
        {
            await HandlePairResponse(device, pairMessage);
        }
        else if (pairMessage.Pair)
        {
            await HandlePairRequest(device);
        }
    }

    #endregion

    #region Server Authentication

    private async void HandleServerSessionAuthentication(ServerSession session, AuthenticationMessage authMessage)
    {
        try
        {
            if (session.Socket.RemoteEndPoint is not IPEndPoint endPoint) return;

            var address = endPoint.Address.ToString();
            logger.Info($"Received connection from {address}");

            var localDevice = await deviceManager.GetLocalDeviceAsync();
            var pairedDevice = PairedDevices.FirstOrDefault(d => d.Id == authMessage.DeviceId);
            
            // Determine shared secret based on whether device is paired
            byte[] sharedSecret = pairedDevice is not null
                ? (await deviceManager.GetDeviceInfoAsync(pairedDevice.Id)).SharedSecret
                : EcdhHelper.DeriveKey(authMessage.PublicKey, localDevice.PrivateKey);

            if (!EcdhHelper.VerifyProof(sharedSecret, authMessage.Nonce, authMessage.Proof))
            {
                throw new Exception("Device proof verification failed");
            }

            // Generate response
            var nonceForResponse = EcdhHelper.GenerateNonce();
            var proofForResponse = EcdhHelper.GenerateProof(sharedSecret, nonceForResponse);
            var authResponse = new AuthenticationMessage
            {
                DeviceId = localDevice.DeviceId,
                DeviceName = localDevice.DeviceName,
                PublicKey = Convert.ToBase64String(localDevice.PublicKey),
                Nonce = nonceForResponse,
                Proof = proofForResponse,
                Model = Environment.MachineName
            };

            if (pairedDevice is not null)
            {
                await AuthenticatePairedDeviceClient(session, pairedDevice, address, authResponse);
                return;
            }
            await AuthenticateNewDeviceClient(session, authMessage, address, sharedSecret, authResponse);
        }
        catch (Exception ex)
        {
            logger.Error($"Error in session authentication: {ex}");
            DisconnectSession(session);
        }
    }

    private async Task AuthenticatePairedDeviceClient(ServerSession session, PairedDevice pairedDevice, string address, AuthenticationMessage authResponse)
    {
        logger.Info($"Paired device {pairedDevice.Name} verified, updating connection");

        // Reject if already connected with a different session
        if (pairedDevice.IsConnected)
        {
            DisconnectDevice(pairedDevice);
        }

        await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            pairedDevice.Session = session;
            pairedDevice.ConnectionStatus = new Connected();
            pairedDevice.Address = address;
            if (!pairedDevice.Addresses.Any(a => a.Address == address))
            {
                var newEntry = new AddressEntry
                {
                    Address = address,
                    IsEnabled = true,
                    Priority = pairedDevice.Addresses.Count
                };
                pairedDevice.Addresses.Add(newEntry);
            }
            deviceManager.ActiveDevice = pairedDevice;
        });

        logger.Debug("Sending auth response");
        SendMessage(session, authResponse);

        // wait a bit after sending auth response
        await Task.Delay(100);

        ConnectionStatusChanged?.Invoke(this, pairedDevice);
    }

    private async Task AuthenticateNewDeviceClient(ServerSession session, AuthenticationMessage authMessage, string address, byte[] sharedSecret, AuthenticationMessage authResponse)
    {
        await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            var existingDiscovered = DiscoveredDevices.FirstOrDefault(d => d.Id == authMessage.DeviceId);
            if (existingDiscovered is not null)
            {
                logger.Info("Device already in DiscoveredDevices, updating session");
                existingDiscovered.Name = authMessage.DeviceName;
                existingDiscovered.Model = authMessage.Model;
                existingDiscovered.Address = address;
                existingDiscovered.SharedSecret = sharedSecret;
                existingDiscovered.Session = session;
            }
            else
            {
                logger.Info($"Adding device {authMessage.DeviceId} as DiscoveredDevice");
                DiscoveredDevices.Add(new DiscoveredDevice
                {
                    Id = authMessage.DeviceId,
                    Name = authMessage.DeviceName,
                    Model = authMessage.Model,
                    Address = address,
                    SharedSecret = sharedSecret,
                    Session = session
                });
            }
        });
        SendMessage(session, authResponse);
    }

    private async Task HandlePairRequest(DiscoveredDevice device)
    {
        logger.Info($"Received pairing request from {device.Name}");

        var tcs = new TaskCompletionSource<bool>();
        await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            try
            {
                var frame = (Frame)App.MainWindow.Content!;
                var dialog = new ConnectionRequestDialog(device.Name, device.SharedSecret, frame)
                {
                    XamlRoot = App.MainWindow.Content!.XamlRoot
                };

                var result = await dialog.ShowAsync();
                tcs.SetResult(result is ContentDialogResult.Primary);
            }
            catch (Exception ex)
            {
                logger.Error($"Error showing connection request dialog: {ex}");
                tcs.SetResult(false);
            }
        });

        var accepted = await tcs.Task;


        device.SendMessage(new PairMessage { Pair = accepted });
        if (accepted)
        {
            var pairedDevice = await deviceManager.AddDevice(device);
            // wait a bit after sending pair responses
            await Task.Delay(100); 
            ConnectionStatusChanged?.Invoke(this, pairedDevice);
        }
    }

    private async Task HandlePairResponse(DiscoveredDevice device, PairMessage pairMessage)
    {
        if (!pairMessage.Pair)
        {
            logger.Info($"Device {device.Name} rejected pairing request");
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.IsPairing = false);
            return;
        }

        logger.Info($"Device {device.Name} accepted pairing request");
        var pairedDevice = await deviceManager.AddDevice(device);
        ConnectionStatusChanged?.Invoke(this, pairedDevice);
    }
    #endregion

    public void DisconnectSession(ServerSession session, bool forcedDisconnect = false)
    {
        logger.Debug($"disconnecing session: {forcedDisconnect}");
        try
        {
            connectionBuffers.TryRemove(session.Id, out _);
            session.Disconnect();
            session.Dispose();
            
            var pairedDevice = PairedDevices.FirstOrDefault(d => d.Session == session);   
            if (pairedDevice is not null)
            {
                pairedDevice.Session = null;
                App.MainWindow.DispatcherQueue.EnqueueAsync(() => pairedDevice.ConnectionStatus = new Disconnected(forcedDisconnect));

                logger.Info($"Device {pairedDevice.Name} session disconnected, status updated");
                ConnectionStatusChanged?.Invoke(this, pairedDevice);
            }
            else
            {
                var discoveredDevice = DiscoveredDevices.FirstOrDefault(d => d.Session == session);
                if (discoveredDevice is not null)
                {
                    App.MainWindow.DispatcherQueue.EnqueueAsync(() => DiscoveredDevices.Remove(discoveredDevice));
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error in Disconnecting: {ex.Message}");
        }
    }

    public void DisconnectClient(Client client, bool forcedDisconnect = false)
    {
        try
        {
            logger.Debug($"disconnecing session, forcedDisconnect: {forcedDisconnect}");
            connectionBuffers.TryRemove(client.Id, out _);

            client.Disconnect();
            client.Dispose();

            var device = PairedDevices.FirstOrDefault(d => d.Client == client);
            if (device is not null)
            {
                device.Client = null;
                App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.ConnectionStatus = new Disconnected(forcedDisconnect));
                ConnectionStatusChanged?.Invoke(this, device);
            }

            var discoveredDevice = DiscoveredDevices.FirstOrDefault(d => d.Client == client);
            if (discoveredDevice is not null)
            {
                App.MainWindow.DispatcherQueue.EnqueueAsync(() => DiscoveredDevices.Remove(discoveredDevice));
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error disconnecting client: {ex.Message}");
        }
    }

    #region Client

    public async Task ConnectTo(string deviceId, string address, int port, string publicKey)
    {
        // Skip if already connected/connecting or if device was force-disconnected
        var existingDevice = PairedDevices.FirstOrDefault(d => d.Id == deviceId);
        if (existingDevice is not null && (existingDevice.IsConnectedOrConnecting || existingDevice.IsForcedDisconnect))
            return;
        
        
        if (DiscoveredDevices.Any(d => d.Id == deviceId)) return;

        lock (connectingDeviceIds)
        {
            if (connectingDeviceIds.Contains(deviceId)) return;
            connectingDeviceIds.Add(deviceId);
        }

        try
        {
            logger.Info($"Connecting to {address}:{port}");

            var client = new Client(SslContext, address, port, this);

            var localDevice = await deviceManager.GetLocalDeviceAsync();

            var sharedSecret = EcdhHelper.DeriveKey(publicKey, localDevice.PrivateKey);
            var nonce = EcdhHelper.GenerateNonce();
            var proof = EcdhHelper.GenerateProof(sharedSecret, nonce);

            var authMessage = new AuthenticationMessage
            {
                DeviceId = localDevice.DeviceId,
                DeviceName = localDevice.DeviceName,
                PublicKey = Convert.ToBase64String(localDevice.PublicKey),
                Nonce = nonce,
                Proof = proof,
                Model = Environment.MachineName
            };

            try
            {
                if (client.ConnectAsync())
                {
                    // Wait for TLS handshake, then send auth
                    if (!client.IsHandshaked)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        handshakeCompletion[client.Id] = tcs;
                        try
                        {
                            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        }
                        finally
                        {
                            handshakeCompletion.TryRemove(client.Id, out _);
                        }
                    }
                    SendMessage(client, authMessage);
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Error connecting to {address}:{port}: {ex.Message}");
            }
        }
        finally
        {
            lock (connectingDeviceIds)
            {
                connectingDeviceIds.Remove(deviceId);
            }
        }
    }

    public async Task ConnectTo(PairedDevice device)
    {
        logger.Info($"Connecting to paired device {device.Name}");

        var remoteDevice = await deviceManager.GetDeviceInfoAsync(device.Id);
        var localDevice = await deviceManager.GetLocalDeviceAsync();

        var nonce = EcdhHelper.GenerateNonce();
        var proof = EcdhHelper.GenerateProof(remoteDevice.SharedSecret, nonce);

        var authMessage = new AuthenticationMessage
        {
            DeviceId = localDevice.DeviceId,
            DeviceName = localDevice.DeviceName,
            PublicKey = Convert.ToBase64String(localDevice.PublicKey),
            Nonce = nonce,
            Proof = proof,
            Model = Environment.MachineName
        };

        foreach (var address in device.GetEnabledAddresses())
        {
            logger.Info($"Connecting to {address}:{device.Port}");
            var client = new Client(SslContext, address, device.Port, this);
            device.ConnectionStatus = new Connecting();

            try
            {
                if (client.ConnectAsync())
                {
                    // Wait for TLS handshake, then send auth
                    if (!client.IsHandshaked)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        handshakeCompletion[client.Id] = tcs;
                        try
                        {
                            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        }
                        finally
                        {
                            handshakeCompletion.TryRemove(client.Id, out _);
                        }
                    }
                    SendMessage(client, authMessage);
                    device.Client = client;
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"Failed to connect to {address}:{device.Port}: {ex.Message}");
            }
        }
        logger.Warn($"Failed to connect to device {device.Name} on any IP address/port combination");
        device.ConnectionStatus = new Disconnected();
    }

    public async Task Pair(DiscoveredDevice device)
    {
        var pairMessage = new PairMessage { Pair = true };

        device.SendMessage(pairMessage);
        device.IsPairing = true;
    }

    #region Client events
    public void OnConnected(Client client)
    {
        logger.Info($"OnConnected for client {client.Id}");
        connectionBuffers[client.Id] = string.Empty;
    }

    public void OnHandshaked(Client client)
    {
        if (handshakeCompletion.TryGetValue(client.Id, out var tcs))
            tcs.TrySetResult(true);
    }

    public void OnDisconnected(Client client)
    {
        if (handshakeCompletion.TryRemove(client.Id, out var tcs))
            tcs.TrySetException(new IOException("Disconnected before TLS handshake completed"));
        if (!connectionBuffers.ContainsKey(client.Id))
        {
            return;
        }
        
        DisconnectClient(client);
    }

    public void OnError(Client client, SocketError error)
    {
        if (handshakeCompletion.TryRemove(client.Id, out var tcs))
            tcs.TrySetException(new IOException($"Socket error before TLS handshake completed: {error}"));
        logger.LogError("Error on client socket {error}", error);
        DisconnectClient(client);
    }

    public void OnReceived(Client client, byte[] buffer, long offset, long size)
    {
        try
        {
            string newData = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            if (!connectionBuffers.TryGetValue(client.Id, out var buffered))
            {
                buffered = string.Empty;
            }

            buffered += newData;

            while (buffered.IndexOf('\n') is var newlineIndex && newlineIndex != -1)
            {
                string messageString = buffered[..newlineIndex].Trim();
                buffered = newlineIndex + 1 >= buffered.Length ? string.Empty : buffered[(newlineIndex + 1)..];

                if (string.IsNullOrEmpty(messageString)) continue;

                logger.Debug($"Processing client message: {(messageString.Length > 100 ? string.Concat(messageString.AsSpan(0, Math.Min(100, messageString.Length)), "...") : messageString)}");

                var socketMessage = JsonMessageSerializer.DeserializeMessage(messageString);
                if (socketMessage is null) continue;

                if (socketMessage is AuthenticationMessage authMessage)
                {
                    HandleServerAuthentication(client, authMessage);
                    return;
                }
                RouteMessage(client.Id, socketMessage);
            }

            connectionBuffers[client.Id] = buffered;
        }
        catch (Exception ex)
        {
            logger.LogError("Error in OnReceived for client: {ex}", ex);
        }
    }
    #endregion

    #region Client Authentication

    private async void HandleServerAuthentication(Client client, AuthenticationMessage authMessage)
    {
        try
        {
            IPEndPoint? endPoint = client.Socket.RemoteEndPoint as IPEndPoint;
            var address = endPoint?.Address.ToString();

            if (string.IsNullOrEmpty(address)) return;

            logger.Info($"Received AuthenticationMessage from server at {address}");

            var pairedDevice = PairedDevices.FirstOrDefault(d => d.Id == authMessage.DeviceId);
            if (pairedDevice is not null)
            {
                await AuthenticatePairedDeviceServer(client, pairedDevice, address, authMessage);
            }
            else
            {
                await AuthenticateNewDeviceServer(client, authMessage, address, endPoint?.Port ?? 5150);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error in client authentication: {ex}");
            DisconnectClient(client);
        }
    }

    private async Task AuthenticatePairedDeviceServer(Client client, PairedDevice pairedDevice, string address, AuthenticationMessage authMessage)
    {
        var remoteDevice = await deviceManager.GetDeviceInfoAsync(pairedDevice.Id);

        if (!EcdhHelper.VerifyProof(remoteDevice.SharedSecret, authMessage.Nonce, authMessage.Proof))
        {
            throw new Exception("Device proof verification failed for client");
        }

        if (pairedDevice.IsConnected && pairedDevice.Client is not null)
        {
            logger.Warn($"Device {pairedDevice.Name} is already connected, declining new connection");
            DisconnectClient(client);
            return;
        }

        await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            pairedDevice.Client = client;
            pairedDevice.Address = address;
            pairedDevice.ConnectionStatus = new Connected();
            deviceManager.ActiveDevice = pairedDevice;
        });

        ConnectionStatusChanged?.Invoke(this, pairedDevice);
        logger.Info($"Paired device {pairedDevice.Name} connected successfully");
    }

    private async Task AuthenticateNewDeviceServer(Client client, AuthenticationMessage authMessage, string address, int port)
    {
        var localDevice = await deviceManager.GetLocalDeviceAsync();
        var sharedSecret = EcdhHelper.DeriveKey(authMessage.PublicKey, localDevice.PrivateKey);

        if (!EcdhHelper.VerifyProof(sharedSecret, authMessage.Nonce, authMessage.Proof))
        {
            logger.Warn("Device proof verification failed for client");
            DisconnectClient(client);
            return;
        }

        await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            var existingDiscovered = DiscoveredDevices.FirstOrDefault(d => d.Address == address);
            if (existingDiscovered is not null)
            {
                logger.Info($"Device with address {address} already in DiscoveredDevices, updating client");
                existingDiscovered.Id = authMessage.DeviceId;
                existingDiscovered.Name = authMessage.DeviceName;
                existingDiscovered.Model = authMessage.Model;
                existingDiscovered.Address = address;
                existingDiscovered.Port = port;
                existingDiscovered.SharedSecret = sharedSecret;
                existingDiscovered.Client = client;
            }
            else
            {
                logger.Info($"Adding device {authMessage.DeviceId} as DiscoveredDevice");
                DiscoveredDevices.Add(new DiscoveredDevice
                {
                    Id = authMessage.DeviceId,
                    Name = authMessage.DeviceName,
                    Model = authMessage.Model,
                    Address = address,
                    Port = port,
                    SharedSecret = sharedSecret,
                    Client = client
                });
            }
        });
    }
    #endregion

    #endregion
}
