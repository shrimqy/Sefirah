using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using Sefirah.Data.Models;

namespace Sefirah.Utils;

/// <summary>
/// Owns an SFTP connection to the active device, rebuilding it whenever the device restarts its
/// server. Each page keeps its own instance so their requests never interleave on one channel.
/// </summary>
public sealed class DeviceSftpConnection
{
    /// <summary>
    /// A per-run key pair used only to authenticate against the device's own SFTP server.
    /// </summary>
    private static readonly Lazy<PrivateKeyFile> SessionKey = new(() =>
    {
        using var rsa = RSA.Create(2048);
        using var pem = new MemoryStream(Encoding.ASCII.GetBytes(rsa.ExportPkcs8PrivateKeyPem()));
        return new PrivateKeyFile(pem);
    });

    private readonly ILogger logger = Ioc.Default.GetRequiredService<ILogger>();
    private readonly IDeviceManager deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    private readonly ISftpFeature sftpFeature = Ioc.Default.GetRequiredService<ISftpFeature>();
    private readonly object gate = new();

    private SftpClient? client;

    /// <summary>
    /// Re-reads the endpoint every time: the device generates a fresh password whenever its
    /// SFTP server restarts, so a cached one goes stale without warning.
    /// </summary>
    public SftpSession? CurrentSession()
    {
        var device = deviceManager.ActiveDevice;
        return device is null ? null : sftpFeature.GetSession(device.Id);
    }

    /// <summary>
    /// Runs an SFTP call off the UI thread, reconnecting once if the device dropped off the network
    /// in the meantime, which it does every time it leaves Wi-Fi.
    /// </summary>
    public Task<T> RunAsync<T>(Func<SftpClient, T> operation) => Task.Run(() =>
    {
        // Only worth retrying a connection that was alive; retrying a fresh connect just doubles the wait
        var hadConnection = client is not null;
        try
        {
            return operation(Connect());
        }
        catch (Exception ex) when (hadConnection && ex is SshException or ObjectDisposedException or IOException or SocketException)
        {
            logger.Warn($"SFTP call failed, reconnecting once: {ex.Message}");
            Drop();
            return operation(Connect());
        }
    });

    public void Drop()
    {
        lock (gate)
        {
            try
            {
                client?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to close the SFTP connection: {ex.Message}", ex);
            }
            client = null;
        }
    }

    /// <summary>
    /// True when the failure means the device is off the network rather than the call being wrong.
    /// A device that walks away gives no notification, the connection attempt simply times out.
    /// </summary>
    public static bool IsUnreachable(Exception ex)
        => ex is SshOperationTimeoutException or SshConnectionException or SocketException or IOException;

    private SftpClient Connect()
    {
        lock (gate)
        {
            if (client is { IsConnected: true }) return client;

            var current = CurrentSession()
                ?? throw new InvalidOperationException("The device has not announced an SFTP endpoint");

            client?.Dispose();
            logger.Info($"Connecting to SFTP {current.Host}:{current.Port} as {current.Username}");

            // The announced password goes stale as soon as the device restarts its SFTP server, and the
            // server accepts any public key, so keep a throwaway key as a fallback for that window.
            var connectionInfo = new ConnectionInfo(current.Host, current.Port, current.Username,
                new PasswordAuthenticationMethod(current.Username, current.Password),
                new PrivateKeyAuthenticationMethod(current.Username, SessionKey.Value))
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var sftp = new SftpClient(connectionInfo)
            {
                OperationTimeout = TimeSpan.FromSeconds(30),
                KeepAliveInterval = TimeSpan.FromSeconds(15)
            };
            sftp.Connect();
            client = sftp;
            return sftp;
        }
    }
}
