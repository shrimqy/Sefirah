using NetCoreServer;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;
using Sefirah.App.Services.Socket;
using Sefirah.App.Utils;
using Sefirah.App.Utils.Serialization;
using Sefirah.Common.Utils;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Sefirah.App.Services;
public class NetworkService(
    Func<IMessageHandlerService> messageHandlerFactory,
    IDeviceManager deviceManager,
    IDiscoveryService discoveryService,
    ILogger logger) : INetworkService, ITcpServerProvider, ISessionManager, IDisposable
{
    private readonly Lazy<IMessageHandlerService> messageHandler = new(messageHandlerFactory);
    private Server? server;
    private bool isRunning;
    private int port;
    private ServerSession? currentSession;
    private bool disposed;
    private X509Certificate2? certificate;

    private string bufferedData = string.Empty;
    private bool isFirstMessage = true;
    private bool isVerified;

    public event EventHandler<ConnectedSessionEventArgs>? ClientConnectionStatusChanged;

    public bool IsConnected() => currentSession != null;

    /// <inheritdoc/>
    public async Task<bool> StartServerAsync()
    {
        if (isRunning)
        {
            logger.Warn("Server is already running");
            return false;
        }
        try
        {

            port = await NetworkHelper.FindAvailablePortAsync(5941);
            certificate = await CertificateHelper.GetOrCreateCertificateAsync();

            var context = new SslContext(SslProtocols.Tls12, certificate);

            server = new Server(context, IPAddress.Any, port, this, logger)
            {
                OptionDualMode = true,
                OptionReuseAddress = true,
            };

            if (server != null)
            {
                isRunning = server.Start();
                // Advertise the specific address clients should connect to
                await discoveryService.StartDiscoveryAsync(port);

                logger.Info($"Server start on port: {port}");
            }

            if (isRunning)
            {
                return true;
            }

            logger.Error("Failed to start server");
            return false;
        }
        catch (Exception ex)
        {
            logger.Error("Error starting server", ex);
            return false;
        }
    }

    public void StopServer()
    {
        if (!isRunning || server == null)
        {
            logger.Debug("Stop server called when server was not running");
            return;
        }

        try
        {
            DisconnectSession();
            server.Stop();
            isRunning = false;
            logger.Info("Server stopped successfully");
        }
        catch (Exception ex)
        {
            logger.Error("Error stopping server", ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public void SendMessage(string message)
    {
        try
        {
            if (currentSession != null)
            {
                string messageWithNewline = message + "\n";
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageWithNewline);

                currentSession.Send(messageBytes, 0, messageBytes.Length);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error sending message", ex);
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
            logger.Info("Disposing SocketService");
            StopServer();
            server?.Dispose();
        }

        disposed = true;
    }

    /// <inheritdoc/>
    public void DisconnectSession(bool removeSession = false)
    {
        try
        {
            isFirstMessage = true;
            isVerified = false;
            bufferedData = string.Empty;

            try
            {
                currentSession?.Disconnect();
                ClientConnectionStatusChanged?.Invoke(this, new ConnectedSessionEventArgs
                {
                    IsConnected = false
                });
            }
            catch (Exception ex)
            {
                logger.Debug($"Non-critical error during session disconnect: {ex.Message}");
            }
            currentSession = null;

            if (removeSession)
            {
                try
                {
                    ClientConnectionStatusChanged?.Invoke(this, new ConnectedSessionEventArgs
                    {
                        Device = null,
                    });
                }
                catch (Exception ex)
                {
                    logger.Error($"Error invoking ClientConnectionStatusChanged: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error in DisconnectSession: {ex.Message}");
        }
    }

    public void OnConnected(ServerSession session)
    {
        currentSession = session;
    }

    public void OnError(SocketError error)
    {
        logger.Error("Server encountered error: {0}", error);
    }

    public async void OnReceived(ServerSession session, byte[] buffer, long offset, long size)
    {
        try
        {
            currentSession = session;
            string newData = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            logger.Debug($"Received raw data: {(newData.Length > 100 ? string.Concat(newData.AsSpan(0, Math.Min(100, newData.Length)), "...") : newData)}");

            bufferedData += newData;

            while (true)
            {
                int newlineIndex = bufferedData.IndexOf('\n');
                if (newlineIndex == -1)
                {
                    break;
                }

                string message = bufferedData[..newlineIndex].Trim();
                bufferedData = bufferedData[(newlineIndex + 1)..];

                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                if (isFirstMessage)
                {
                    await HandleFirstMessage(session, message);
                    continue;
                }

                if (!isVerified)
                {
                    logger.Warn("Unverified session {0} attempted to send message", currentSession!.Id);
                    DisconnectSession(true);
                    return;
                }

                await ProcessMessage(message);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error in OnReceived for session {0}: {1}", session.Id, ex);
            DisconnectSession(true);
        }
    }

    private async Task HandleFirstMessage(ServerSession session, string message)
    {
        try
        {
            if (SocketMessageSerializer.DeserializeMessage(message) is not DeviceInfo deviceInfo)
            {
                logger.Warn("Invalid device info or first message wasn't deviceInfo");
                DisconnectSession(true);
                return;
            }

            // Verify authentication data
            if (string.IsNullOrEmpty(deviceInfo.Nonce) || string.IsNullOrEmpty(deviceInfo.Proof))
            {
                logger.Warn("Missing authentication data");
                DisconnectSession(true);
                return;
            }

            var device = await deviceManager.VerifyDevice(deviceInfo);

            if (device != null)
            {
                isFirstMessage = false;
                isVerified = true;
                currentSession = session;
                await SendDeviceInfo(deviceInfo.PublicKey!);
                NotifyClientConnectionChanged(device);
            }
            else
            {
                logger.Info("Device verification failed or was declined");
                DisconnectSession();
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error processing first message for session {session.Id}: {ex}");
            DisconnectSession(true);
        }
    }

    private async Task SendDeviceInfo(string remotePublicKey)
    {
        var (_, avatar) = await CurrentUserInformation.GetCurrentUserInfoAsync();
        var localDevice = await deviceManager.GetLocalDeviceAsync();
        
        // Generate our authentication proof
        var sharedSecret = EcdhHelper.DeriveKey(remotePublicKey, localDevice.PrivateKey);
        var nonce = EcdhHelper.GenerateNonce();
        var proof = EcdhHelper.GenerateProof(sharedSecret, nonce);

        SendMessage(SocketMessageSerializer.Serialize(new DeviceInfo
        {
            DeviceId = localDevice.DeviceId,
            DeviceName = localDevice.DeviceName,
            Avatar = avatar,
            PublicKey = Convert.ToBase64String(localDevice.PublicKey),
            Nonce = nonce,
            Proof = proof
        }));
    }

    private void NotifyClientConnectionChanged(RemoteDeviceEntity device)
    {
        var args = new ConnectedSessionEventArgs
        {
            Device = device,
            SessionId = currentSession?.Id.ToString(),
            IsConnected = true
        };
        ClientConnectionStatusChanged?.Invoke(this, args);
    }

    private async Task ProcessMessage(string message)
    {
        try
        {
            logger.Debug($"Processing individual message: {(message.Length > 100 ? string.Concat(message.AsSpan(0, Math.Min(100, message.Length)), "...") : message)}");
            var socketMessage = SocketMessageSerializer.DeserializeMessage(message);
            if (socketMessage != null)
                await messageHandler.Value.HandleJsonMessage(socketMessage);
        }
        catch (JsonException jsonEx)
        {
            logger.Error($"Error parsing JSON message: {jsonEx.Message}");
        }
    }

    public void OnDisconnected(ServerSession session)
    {
        DisconnectSession();
    }
}
