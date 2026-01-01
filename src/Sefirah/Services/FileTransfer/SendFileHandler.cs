using System.Net;
using System.Net.Sockets;
using System.Text;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Services.Socket;

namespace Sefirah.Services.FileTransfer;

public partial class SendFileHandler(
    StorageFile[] storageFiles,
    List<FileMetadata> files,
    PairedDevice device,
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
    private TaskCompletionSource<ServerSession>? connectionSource;
    private TaskCompletionSource<bool>? transferCompletionSource;
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
                transferCompletionSource = new TaskCompletionSource<bool>();

                await SendFileData(files[i], await storageFiles[i].OpenStreamForReadAsync());
                await transferCompletionSource.Task;

                currentFileIndex++;
            }

            // Show completion notification
            if (IsBulkTransfer)
            {
                notificationHandler.ShowCompletedFileTransferNotification(
                    string.Format("FileTransferNotification.SentBulk".GetLocalizedResource(), files.Count, device.Name),
                    TransferId.ToString());
            }
            else
            {
                notificationHandler.ShowCompletedFileTransferNotification(
                    string.Format("FileTransferNotification.SentSingle".GetLocalizedResource(), files[0].FileName, device.Name),
                    TransferId.ToString());
            }

            logger.Debug("All files transferred successfully");
        }
        catch (OperationCanceledException)
        {
            logger.Info("File transfer cancelled");
            notificationHandler?.RemoveNotificationByTag(TransferId.ToString());
        }
        catch (Exception ex)
        {
            logger.Error("Error in SendFileHandler", ex);
            notificationHandler?.RemoveNotificationByTag(TransferId.ToString());
        }
    }

    public void Cancel()
    {
        cancellationTokenSource.Cancel();
    }

    private async Task SendFileData(FileMetadata metadata, Stream stream)
    {
        try
        {
            using (stream)
            {
                var buffer = new byte[FileTransferService.ChunkSize];

                while (bytesTransferred < metadata.FileSize && session!.IsConnected)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    int bytesRead = await stream.ReadAsync(buffer);
                    if (bytesRead == 0) break;

                    session.Send(buffer, 0, bytesRead);
                    bytesTransferred += bytesRead;
                    totalBytesTransferred += bytesRead;

                    notificationSequence++;
                    ShowProgressNotification();
                }
            }

            bytesTransferred = 0;
            logger.Info($"Completed file transfer for {metadata.FileName}");
        }
        catch (Exception ex)
        {
            logger.Error("Error in SendFileData", ex);
            throw;
        }
    }

    private void ShowProgressNotification()
    {
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
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private ServerInfo InitializeServer()
    {
        foreach (int port in FileTransferService.PortRange)
        {
            try
            {
                server = new Server(CertificateHelper.SslContext, IPAddress.Any, port, this, logger)
                {
                    OptionDualMode = true,
                };
                server.Start();

                var serverInfo = new ServerInfo
                {
                    Port = port,
                    Password = EcdhHelper.GenerateRandomPassword()
                };

                logger.Info($"File transfer server initialized at {serverInfo.Port}");
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
    }

    public void OnDisconnected(ServerSession session)
    {
        if (transferCompletionSource?.Task.IsCompleted is false)
        {
            transferCompletionSource.TrySetException(new Exception("Client disconnected"));
        }
        logger.Info($"Client disconnected from file transfer server: {session.Id}");
    }

    public void OnReceived(ServerSession session, byte[] buffer, long offset, long size)
    {
        string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

        // Check if this is a password for authentication
        if (serverInfo is not null && message == serverInfo.Password && connectionSource?.Task.IsCompleted is false)
        {
            connectionSource.SetResult(session);
            return;
        }

        // Check for completion message
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
        cancellationTokenSource.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

