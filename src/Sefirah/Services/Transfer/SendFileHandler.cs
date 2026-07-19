using System.Net;
using System.Net.Sockets;
using System.Text;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Services.Socket;

namespace Sefirah.Services.Transfer;

public partial class SendFileHandler(
    StorageFile[] storageFiles,
    List<FileMetadata> files,
    PairedDevice device,
    byte[] expectedClientCert,
    Action<ServerInfo> sendTransferMessage,
    ILogger logger,
    IPlatformNotificationHandler notificationHandler) : ITcpServerProvider, IDisposable
{
    private Server? server;
    private ServerInfo? serverInfo;
    private ServerSession? session;
    private long bytesTransferred;
    private long totalBytesTransferred;
    private readonly long totalBytes = files.Sum(f => f.FileSize);
    private int currentFileIndex;
    private uint notificationSequence = 1;
    private long lastNotificationUpdateTimestamp;
    private TaskCompletionSource<ServerSession>? connectionSource;
    private TaskCompletionSource<bool>? transferCompletionSource;
    private TaskCompletionSource<bool>? startMessageSource;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool disposed;

    public Guid TransferId { get; private set; }
    public double Progress => (double)totalBytesTransferred / totalBytes * 100;
    public bool IsBulkTransfer => files.Count > 1;

    /// <summary>
    /// Starts the server and waits for the remote device to connect.
    /// </summary>
    /// <returns>The transfer ID for tracking this transfer.</returns>
    public async Task<Guid> WaitForConnectionAsync()
    {
        // Initialize server
        serverInfo = InitializeServer();

        // Let caller create and send the appropriate message type
        sendTransferMessage(serverInfo);

        // Wait for client to connect
        connectionSource = new TaskCompletionSource<ServerSession>();
        session = await connectionSource.Task;
        TransferId = session.Id;

        return TransferId;
    }

    /// <summary>
    /// Sends files to the connected client.
    /// </summary>
    public async Task SendAsync()
    {
        try
        {
            // Show initial notification
            ShowProgressNotification();

            // Send each file
            for (int i = 0; i < storageFiles.Length; i++)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                logger.Debug($"Sending file: {files[i].FileName}");
                
                // Wait for client to send "start" message indicating readiness to receive
                startMessageSource = new TaskCompletionSource<bool>();
                await startMessageSource.Task;
                
                transferCompletionSource = new TaskCompletionSource<bool>();
                await SendFileData(files[i], await storageFiles[i].OpenStreamForReadAsync());
                await transferCompletionSource.Task;

                currentFileIndex++;
            }

            // Show completion notification
            if (IsBulkTransfer)
            {
                notificationHandler.ShowCompletedFileTransferNotification(
                    "FileTransferNotification.Completed".GetLocalizedResource(),
                    string.Format("FileTransferNotification.SentBulk".GetLocalizedResource(), files.Count, device.Name),
                    TransferId.ToString());
            }
            else
            {
                notificationHandler.ShowCompletedFileTransferNotification(
                    "FileTransferNotification.Completed".GetLocalizedResource(),
                    string.Format("FileTransferNotification.SentSingle".GetLocalizedResource(), files[0].FileName, device.Name),
                    TransferId.ToString());
            }

            logger.Debug("All files transferred successfully");
        }
        catch (OperationCanceledException)
        {
            logger.Info($"File transfer to {device.Name} cancelled");
            _ = notificationHandler.RemoveNotificationsByTagAndGroup(TransferId.ToString(), Constants.Notification.FileTransferGroup);
        }
        catch (Exception ex)
        {
            logger.Warn($"File transfer to {device.Name} failed: {ex.Message}");
            notificationHandler.ShowCompletedFileTransferNotification(
                "FileTransferNotification.Failed".GetLocalizedResource(),
                string.Format("FileTransferNotification.FailedTo".GetLocalizedResource(), device.Name),
                TransferId.ToString());
        }
    }

    public void Cancel()
    {
        cancellationTokenSource.Cancel();
        startMessageSource?.TrySetCanceled(cancellationTokenSource.Token);
        transferCompletionSource?.TrySetCanceled(cancellationTokenSource.Token);
    }

    private async Task SendFileData(FileMetadata metadata, Stream stream)
    {
        using (stream)
        {
            var buffer = new byte[FileTransferService.ChunkSize];

            while (bytesTransferred < metadata.FileSize)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                // Remote disconnect is a failure, not a cancellation.
                if (session is null || !session.IsConnected)
                    throw new IOException("Remote device disconnected during transfer");

                int bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                session.Send(buffer, 0, bytesRead);
                bytesTransferred += bytesRead;
                totalBytesTransferred += bytesRead;

                ShowProgressNotification();
            }
        }

        bytesTransferred = 0;
        logger.Info($"Completed file transfer for {metadata.FileName}");
    }

    private void ShowProgressNotification()
    {
        var now = Environment.TickCount64;
        if (lastNotificationUpdateTimestamp != 0 && now - lastNotificationUpdateTimestamp < 500)
            return;

        if (lastNotificationUpdateTimestamp != 0)
            notificationSequence++;

        lastNotificationUpdateTimestamp = now;

        var fileName = files[Math.Min(currentFileIndex, files.Count - 1)].FileName;

        // Title: fileName (index/total)
        var progressTitle = $"{fileName} ({currentFileIndex + 1}/{files.Count})";

        // Notification title: Receiving/Sending message
        var notificationTitle = IsBulkTransfer
            ? string.Format("FileTransferNotification.SendingBulk".GetLocalizedResource(), files.Count, device.Name)
            : string.Format("FileTransferNotification.Sending".GetLocalizedResource(), device.Name);

        // Status: "{transferred} / {total}"
        var transferredFormatted = FormatBytes(totalBytesTransferred);
        var totalFormatted = FormatBytes(totalBytes);
        var status = $"{transferredFormatted} / {totalFormatted}";

        notificationHandler.ShowFileTransferNotification(
            notificationTitle,
            progressTitle,
            status,
            TransferId.ToString(),
            notificationSequence,
            Progress);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private ServerInfo InitializeServer()
    {
        var sslContext = SslHelper.CreateSslContext(expectedClientCert);
        foreach (int port in FileTransferService.PortRange)
        {
            try
            {
                server = new Server(sslContext, IPAddress.IPv6Any, port, this)
                {
                    OptionDualMode = true,
                };
                server.Start();

                serverInfo = new ServerInfo(port);
                logger.Info($"File transfer server initialized at {serverInfo.Port} (cert-only auth)");
                return serverInfo;
            }
            catch (Exception ex)
            {
                server?.Dispose();
                server = null;
                logger.Debug($"Failed to start server on port {port}: {ex.Message}");
            }
        }

        throw new IOException("Failed to start file transfer server: all ports in range are unavailable");
    }

    #region ITcpServerProvider Implementation

    public void OnConnected(ServerSession session)
    {
        logger.Info($"Client connected to file transfer server: {session.Id}");
        if (connectionSource?.Task.IsCompleted is false)
            connectionSource.SetResult(session);
    }

    public void OnDisconnected(ServerSession session)
    {
        logger.Info($"Client disconnected from file transfer server: {session.Id}");

        // Unblock any pending waits so the transfer stops instead of hanging.
        transferCompletionSource?.TrySetException(new IOException("Remote device disconnected during transfer"));
        startMessageSource?.TrySetException(new IOException("Remote device disconnected before transfer started"));
    }

    public void OnReceived(ServerSession session, byte[] buffer, long offset, long size)
    {
        string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

        if (message == FileTransferService.StartMessage && startMessageSource?.Task.IsCompleted is false)
        {
            startMessageSource.TrySetResult(true);
            return;
        }

        if (message == FileTransferService.CompleteMessage)
        {
            transferCompletionSource?.TrySetResult(true);
        }
    }

    public void OnError(SocketError error)
    {
        logger.Error($"Server socket error: {error}");
    }

    #endregion

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        server?.Stop();
        server?.Dispose();
        server = null;
        session = null;
        serverInfo = null;
        connectionSource = null;
        transferCompletionSource = null;
        startMessageSource = null;
        cancellationTokenSource.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

