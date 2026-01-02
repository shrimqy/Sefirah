using System.Net.Sockets;
using System.Text;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Services.Socket;

namespace Sefirah.Services.FileTransfer;

public partial class ReceiveFileHandler(
    List<FileMetadata> files,
    ServerInfo serverInfo,
    PairedDevice device,
    string storageLocation,
    ILogger logger,
    IPlatformNotificationHandler notificationHandler) : ITcpClientProvider, IDisposable
{
    private Client? client;
    private FileStream? fileStream;
    private FileMetadata? currentFileMetadata;
    private long bytesTransferred;
    private long totalBytesTransferred = 0;
    private readonly long totalBytes = files.Sum(f => f.FileSize);
    private int currentFileIndex = 0;
    private uint notificationSequence = 1;
    private TaskCompletionSource<bool>? handshakeTcs;
    private TaskCompletionSource<bool>? transferCompletionSource;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool disposed;

    public Guid TransferId { get; private set; }
    public double Progress => (double)totalBytesTransferred / totalBytes * 100;
    public bool IsBulkTransfer => files.Count > 1;

    /// <summary>
    /// Connects to the file transfer server and authenticates.
    /// </summary>
    /// <returns>The transfer ID for tracking this transfer.</returns>
    public async Task<Guid> ConnectAsync()
    {
        client = new Client(CertificateHelper.SslContext, device.Address, serverInfo.Port, this);
        TransferId = client.Id;

        if (!client.ConnectAsync())
            throw new IOException("Failed to connect to file transfer server");

        // Wait for TLS handshake
        if (!client.IsHandshaked)
        {
            handshakeTcs = new TaskCompletionSource<bool>();
            await handshakeTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        // Send password to authenticate
        var passwordBytes = Encoding.UTF8.GetBytes(serverInfo.Password + "\n");
        client.SendAsync(passwordBytes);

        return TransferId;
    }

    /// <summary>
    /// Receives files from the connected server.
    /// </summary>
    /// <returns>The received file for single file transfers, null for bulk.</returns>
    public async Task<StorageFile?> ReceiveAsync()
    {
        StorageFile? resultFile = null;
        
        try
        {
            // Show initial notification
            ShowProgressNotification();

            // Process each file
            foreach (var fileMetadata in files)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                logger.Info($"Starting to receive file {currentFileIndex + 1}/{files.Count}: {fileMetadata.FileName}");

                // Wait for previous file to complete (for bulk transfers)
                if (transferCompletionSource?.Task is { IsCompleted: false })
                {
                    await transferCompletionSource.Task;
                }

                string fullPath = Path.Combine(storageLocation, fileMetadata.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                transferCompletionSource = new TaskCompletionSource<bool>();
                currentFileMetadata = fileMetadata;
                fileStream = new FileStream(fullPath, FileMode.Create);

                // Wait for this file transfer to complete
                await transferCompletionSource.Task;

                currentFileIndex++;
                logger.Info($"Received file {fileMetadata.FileName}");

                // For single file transfer, capture the result
                if (!IsBulkTransfer)
                {
                    resultFile = await StorageFile.GetFileFromPathAsync(fullPath);
                }

                CleanupFileStream();
            }

            // Show completion notification
            if (IsBulkTransfer)
            {
                notificationHandler.ShowCompletedFileTransferNotification(
                    string.Format("FileTransferNotification.CompletedBulk".GetLocalizedResource(), files.Count, device.Name),
                    TransferId.ToString(),
                    folderPath: storageLocation);
            }
            else
            {
                notificationHandler.ShowCompletedFileTransferNotification(
                    string.Format("FileTransferNotification.CompletedSingle".GetLocalizedResource(), files[0].FileName, device.Name),
                    TransferId.ToString(),
                    Path.Combine(storageLocation, files[0].FileName));
            }

            logger.Info($"File transfer completed: {currentFileIndex}/{files.Count} files received");
        }
        catch (OperationCanceledException)
        {
            logger.Info("File transfer cancelled");
            CleanupFailedFile();
        }
        catch (Exception ex)
        {
            logger.Error("Error during file transfer", ex);
            CleanupFailedFile();
        }

        return resultFile;
    }

    public void Cancel()
    {
        cancellationTokenSource.Cancel();
    }

    private void ShowProgressNotification()
    {
        var fileName = currentFileMetadata?.FileName ?? files[0].FileName;

        // Title: fileName (index/total)
        var progressTitle = $"{fileName} ({currentFileIndex + 1}/{files.Count})";

        // Subtitle: Receiving/Sending message
        var notifcationTitle = IsBulkTransfer
            ? string.Format("FileTransferNotification.ReceivingBulk".GetLocalizedResource(), files.Count, device.Name)
            : string.Format("FileTransferNotification.Receiving".GetLocalizedResource(), device.Name);

        // Status: "{transferred} / {total}"
        var transferredFormatted = FormatBytes(totalBytesTransferred);
        var totalFormatted = FormatBytes(totalBytes);
        var status = $"{transferredFormatted} / {totalFormatted}";

        notificationHandler.ShowFileTransferNotification(
            notifcationTitle,
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

    private void CleanupFileStream()
    {
        fileStream?.Close();
        fileStream?.Dispose();
        fileStream = null;
        currentFileMetadata = null;
        bytesTransferred = 0;
    }

    private void CleanupFailedFile()
    {
        notificationHandler.RemoveNotificationByTag(TransferId.ToString());
        
        // Save file metadata before cleanup
        var fileToDelete = currentFileMetadata;
        
        CleanupFileStream();
        
        // Delete the incomplete file if it exists
        if (fileToDelete is not null)
        {
            var failedFilePath = Path.Combine(storageLocation, fileToDelete.FileName);
            if (File.Exists(failedFilePath))
            {
                try
                {
                    File.Delete(failedFilePath);
                    logger.Info($"Deleted incomplete file: {fileToDelete.FileName}");
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to delete incomplete file: {fileToDelete.FileName}", ex);
                }
            }
        }
    }

    #region ITcpClientProvider Implementation

    public void OnConnected(Client client)
    {
        logger.Info("Connected to file transfer server");
    }

    public void OnDisconnected(Client client)
    {
        logger.Info("Disconnected from file transfer server");

        handshakeTcs?.TrySetException(new IOException("Disconnected before TLS handshake completed"));

        // If transfer is not complete
        if (currentFileMetadata is not null && fileStream is not null && bytesTransferred < currentFileMetadata.FileSize)
        {
            transferCompletionSource?.TrySetException(new IOException("Connection to server lost"));
        }
    }

    public void OnError(Client client, SocketError error)
    {
        logger.Error($"Socket error occurred during file transfer: {error}");
        handshakeTcs?.TrySetException(new IOException($"Socket error before TLS handshake completed: {error}"));
        transferCompletionSource?.TrySetException(new IOException($"Socket error: {error}"));
    }

    public void OnHandshaked(Client client)
    {
        handshakeTcs?.TrySetResult(true);
    }

    public void OnReceived(Client client, byte[] buffer, long offset, long size)
    {
        try
        {
            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            if (fileStream is null || currentFileMetadata is null) return;

            fileStream.Write(buffer, (int)offset, (int)size);
            bytesTransferred += size;
            totalBytesTransferred += size;

            notificationSequence++;
            ShowProgressNotification();

            if (bytesTransferred >= currentFileMetadata.FileSize)
            {
                logger.Info($"File {currentFileMetadata.FileName} received successfully");
                transferCompletionSource?.TrySetResult(true);
                client.Send(Encoding.UTF8.GetBytes(FileTransferService.CompleteMessage + "\n"));
                bytesTransferred = 0;
            }
        }
        catch (Exception ex)
        {
            transferCompletionSource?.TrySetException(ex);
        }
    }

    #endregion

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        CleanupFileStream();
        client?.DisconnectAsync();
        client?.Dispose();
        client = null;
        handshakeTcs = null;
        transferCompletionSource = null;
        cancellationTokenSource.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

