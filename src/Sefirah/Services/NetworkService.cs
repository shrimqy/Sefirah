using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CommunityToolkit.WinUI;
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

    private static readonly IEnumerable<int> PORT_RANGE = Enumerable.Range(5150, 20); // 5150 to 5169

    private readonly ConcurrentDictionary<Guid, StringBuilder> connectionBuffers = [];
    private readonly HashSet<string> connectingDeviceIds = [];
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> handshakeCompletion = [];
    private readonly ConcurrentDictionary<string, CancellationTokenSource> connectionCancellationTokens = [];

    private ObservableCollection<PairedDevice> PairedDevices => deviceManager.PairedDevices;
    private ObservableCollection<DiscoveredDevice> DiscoveredDevices => deviceManager.DiscoveredDevices;

    /// <summary>
    /// Event fired when a device connection status changes
    /// </summary>
    public event EventHandler<PairedDevice>? ConnectionStatusChanged;

    public async Task StartServerAsync()
    {
        if (isRunning) return;

        ConnectionStatusChanged += ConnectionStatusChangedEvent;

        foreach (int port in PORT_RANGE)
        {
            try
            {
                server = new Server(SslHelper.GetSslContext(), IPAddress.Any, port, this, logger)
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
            logger.Error("Exception occurred while sending device info", ex);
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

    public void BroadcastMessage(SocketMessage message)
    {
        if (PairedDevices.Count == 0) return;

        foreach (var device in PairedDevices.Where(d => d.IsConnected))
        {
            device.SendMessage(message);
        }
    }

    private static byte[] EncodeMessage(SocketMessage message) =>
        Encoding.UTF8.GetBytes(JsonMessageSerializer.Serialize(message) + "\n");

    /// <summary>
    /// Extracts complete newline-delimited messages from the buffer, deserializes them, removes from buffer, returns list.
    /// </summary>
    private static List<SocketMessage> GetMessagesFromBuffer(StringBuilder sb, ILogger logger)
    {
        List<SocketMessage> messages = [];
        while (sb.Length > 0)
        {
            int newlineIndex = -1;
            for (int i = 0; i < sb.Length; i++)
            {
                if (sb[i] == '\n')
                {
                    newlineIndex = i; break;
                }
            }
            if (newlineIndex < 0) break;

            var messageString = sb.ToString(0, newlineIndex).Trim();
            sb.Remove(0, newlineIndex + 1);

            if (string.IsNullOrEmpty(messageString)) continue;

            logger.LogDebug("Processing message: {Preview}", messageString.Length > 100 ? string.Concat(messageString.AsSpan(0, 100), "...") : messageString);

            var socketMessage = JsonMessageSerializer.DeserializeMessage(messageString);
            if (socketMessage is not null)
            {
                messages.Add(socketMessage);
            }
        }
        return messages;
    }

    #region Server events
    public void OnConnected(ServerSession session) 
    {
        connectionBuffers[session.Id] = new StringBuilder();
    }

    public void OnDisconnected(ServerSession session)
    {
        if (!connectionBuffers.ContainsKey(session.Id)) return;

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
            var sb = connectionBuffers.GetOrAdd(session.Id, _ => new StringBuilder());
            sb.Append(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

            foreach (var socketMessage in GetMessagesFromBuffer(sb, logger))
            {
                if (socketMessage is Authentication authMessage)
                {
                    HandleServerSessionAuthentication(session, authMessage);
                    return;
                }
                RouteMessage(session.Id, socketMessage);
            }
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
                if (message is ConnectionAck)
                {
                    ConnectionStatusChanged?.Invoke(this, pairedDevice);
                    return;
                }
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

    private async void HandleServerSessionAuthentication(ServerSession session, Authentication authMessage)
    {
        try
        {
            if (session.Socket.RemoteEndPoint is not IPEndPoint endPoint) return;

            var address = endPoint.Address.ToString();
            logger.Info($"Received connection from {address}");

            // Server-side cert-based auth: connecting client sends PublicKey; we look up the stashed cert
            var cert = SslHelper.GetCertForPublicKey(authMessage.PublicKey);
            if (cert is null || cert.Length == 0)
            {
                logger.Warn("No client certificate or PublicKey mismatch; rejecting connection");
                throw new Exception("Client certificate required");
            }


            var pairedDevice = PairedDevices.FirstOrDefault(d => d.Id == authMessage.DeviceId);
            if (pairedDevice is not null)
            {
                if (pairedDevice.Certificate.Length == 0 || cert.Length != pairedDevice.Certificate.Length || !cert.AsSpan().SequenceEqual(pairedDevice.Certificate))
                {
                    throw new Exception("Certificate verification failed for paired device");
                }
                await AuthenticatePairedDeviceClient(session, pairedDevice, address);
                return;
            }

            await AuthenticateNewDeviceClient(session, authMessage, address, cert);
        }
        catch (Exception ex)
        {
            logger.Error($"Error in session authentication: {ex}");
            DisconnectSession(session);
        }
    }

    private async Task AuthenticatePairedDeviceClient(ServerSession session, PairedDevice pairedDevice, string address)
    {
        logger.Info($"Paired device {pairedDevice.Name} verified, updating connection");

        if (pairedDevice.IsConnected && pairedDevice.Session is not null)
        {
            DisconnectSession(pairedDevice.Session);
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
    }

    private async Task AuthenticateNewDeviceClient(ServerSession session, Authentication authMessage, string address, byte[] certificate)
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
                existingDiscovered.Certificate = certificate;
                existingDiscovered.VerificationKey = SslHelper.GetVerificationCode(authMessage.PublicKey);
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
                    Certificate = certificate,
                    VerificationKey = SslHelper.GetVerificationCode(authMessage.PublicKey),
                    Session = session
                });
            }
        });

        var localDevice = await deviceManager.GetLocalDeviceAsync();

        var authResponse = new Authentication
        {
            DeviceId = localDevice.DeviceId,
            DeviceName = localDevice.DeviceName,
            PublicKey = SslHelper.DevicePublicKeyString,
            Model = Environment.MachineName
        };

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
                var dialog = new ConnectionRequestDialog(device.Name, device.VerificationKey, frame)
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
        if (accepted) await deviceManager.AddDevice(device);
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

    public async Task ConnectTo(string deviceId, string address, int port)
    {
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
            logger.Info($"Connecting to {address}:{port} (discovery, accept and stash server cert)");

            var client = new Client(SslHelper.GetSslContext(), address, port, this);

            var localDevice = await deviceManager.GetLocalDeviceAsync();
            var authMessage = new Authentication
            {
                DeviceId = localDevice.DeviceId,
                DeviceName = localDevice.DeviceName,
                PublicKey = SslHelper.DevicePublicKeyString,
                Model = Environment.MachineName
            };

            try
            {
                if (client.ConnectAsync())
                {
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
        if (connectionCancellationTokens.TryRemove(device.Id, out var removedCts))
        {
            removedCts.Cancel();
            removedCts.Dispose();
            device.ConnectionStatus = new Disconnected();
            return;
        }

        logger.Info($"Connecting to paired device {device.Name}");

        var cts = new CancellationTokenSource();
        connectionCancellationTokens[device.Id] = cts;

        try
        {
            var localDevice = await deviceManager.GetLocalDeviceAsync();
            var authMessage = new Authentication
            {
                DeviceId = localDevice.DeviceId,
                DeviceName = localDevice.DeviceName,
                PublicKey = SslHelper.DevicePublicKeyString,
                Model = Environment.MachineName
            };

            foreach (var address in device.GetEnabledAddresses())
            {
                cts.Token.ThrowIfCancellationRequested();

                var clientContext = SslHelper.CreateSslContext(device.Certificate);

                logger.Info($"Connecting to {address}:{device.Port}");
                var client = new Client(clientContext, address, device.Port, this);
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
                                using (cts.Token.Register(() => tcs.TrySetCanceled()))
                                {
                                    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);
                                }
                            }
                            finally
                            {
                                handshakeCompletion.TryRemove(client.Id, out _);
                            }
                        }
                        
                        cts.Token.ThrowIfCancellationRequested();
                        
                        SendMessage(client, authMessage);
                        device.Client = client;
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Debug($"Failed to connect to {address}:{device.Port}: {ex.Message}");
                }
            }
            logger.Warn($"Failed to connect to device {device.Name} on any IP address/port combination");
        }
        catch (OperationCanceledException)
        {
            logger.Info($"Connection attempt cancelled for device {device.Name}");
        }
        finally
        {
            device.ConnectionStatus = new Disconnected();

            if (connectionCancellationTokens.TryRemove(device.Id, out var cancellationTokenSource))
            {
                cancellationTokenSource.Dispose();
            }
        }
    }

    public async Task Pair(DiscoveredDevice device)
    {
        device.SendMessage(new PairMessage { Pair = true });
        device.IsPairing = true;
    }

    #region Client events
    public void OnConnected(Client client)
    {
        logger.Info($"OnConnected for client {client.Id}");
        connectionBuffers[client.Id] = new StringBuilder();
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
            var sb = connectionBuffers.GetOrAdd(client.Id, _ => new StringBuilder());
            sb.Append(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

            foreach (var socketMessage in GetMessagesFromBuffer(sb, logger))
            {
                if (socketMessage is Authentication authMessage)
                {
                    HandleServerAuthentication(client, authMessage);
                    return;
                }
                RouteMessage(client.Id, socketMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Error in OnReceived for client: {ex}", ex);
        }
    }
    #endregion

    #region Client Authentication

    private async void HandleServerAuthentication(Client client, Authentication authMessage)
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
                // New device: we stashed the server cert during TLS; look it up by PublicKey and store on DiscoveredDevice.
                var certificate = SslHelper.GetCertForPublicKey(authMessage.PublicKey);
                if (certificate is null || certificate.Length == 0)
                {
                    throw new Exception("No server certificate or PublicKey mismatch; rejecting");
                }
                await AuthenticateNewDeviceServer(client, authMessage, address, endPoint?.Port ?? 5150, certificate);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error in client authentication: {ex}");
            DisconnectClient(client);
        }
    }

    private async Task AuthenticatePairedDeviceServer(Client client, PairedDevice pairedDevice, string address, Authentication authMessage)
    {
        if (pairedDevice.IsConnected && pairedDevice.Client is not null)
        {
            logger.Warn($"Device {pairedDevice.Name} is already connected, disconnect the current client");
            DisconnectClient(pairedDevice.Client);
        }

        await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            pairedDevice.Client = client;
            pairedDevice.Address = address;
            pairedDevice.ConnectionStatus = new Connected();
            deviceManager.ActiveDevice = pairedDevice;
        });

        logger.Info($"Paired device {pairedDevice.Name} connected successfully");
    }

    private async Task AuthenticateNewDeviceServer(Client client, Authentication authMessage, string address, int port, byte[] certificate)
    {
        var verificationKey = SslHelper.GetVerificationCode(authMessage.PublicKey);
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
                existingDiscovered.Certificate = certificate;
                existingDiscovered.VerificationKey = verificationKey;
                existingDiscovered.Client = client;
            }
            else
            {
                DiscoveredDevices.Add(new DiscoveredDevice
                {
                    Id = authMessage.DeviceId,
                    Name = authMessage.DeviceName,
                    Model = authMessage.Model,
                    Address = address,
                    Port = port,
                    Certificate = certificate,
                    VerificationKey = verificationKey,
                    Client = client
                });
            }
        });
    }
    #endregion

    #endregion
}
