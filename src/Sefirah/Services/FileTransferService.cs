using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using NetCoreServer;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Dialogs;
using Sefirah.Extensions;
using Sefirah.Helpers;
using Sefirah.Services.Socket;
using Sefirah.Utils.Serialization;
using Uno.Logging;

namespace Sefirah.Services;
public class FileTransferService(
    ILogger logger,
    ISessionManager sessionManager,
    IUserSettingsService userSettingsService,
    IDeviceManager deviceManager,
    IPlatformNotificationHandler notificationHandler
    ) : IFileTransferService, ITcpClientProvider, ITcpServerProvider
{
    private string? storageLocation;
    private FileStream? currentFileStream;
    private FileMetadata? currentFileMetadata;
    private long bytesReceived;
    private Client? client;
    private Server? server;
    private ServerInfo? serverInfo;
    private ServerSession? session;
    private uint notificationSequence = 1;

    private const string COMPLETE_MESSAGE = "Complete";

    private TaskCompletionSource<ServerSession>? connectionSource;
    private TaskCompletionSource<bool>? sendTransferCompletionSource;
    private TaskCompletionSource<bool>? receiveTransferCompletionSource;

    public event EventHandler<(PairedDevice device, StorageFile data)>? FileReceived;

    private readonly IEnumerable<int> PORT_RANGE = Enumerable.Range(5152, 18);

    #region Receive
    public async Task ReceiveBulkFiles(BulkFileTransfer bulkFile, PairedDevice device)
    {
        try
        {
            // Wait for any existing transfer to complete
            if (receiveTransferCompletionSource?.Task is not null)
            {
                await receiveTransferCompletionSource.Task;
            }

            storageLocation = userSettingsService.GeneralSettingsService.ReceivedFilesPath;
            var serverInfo = bulkFile.ServerInfo;
            var totalFiles = bulkFile.Files.Count;
            var receivedFiles = 0;
            var failedFiles = new List<string>();

            // Show initial bulk transfer notification
            await notificationHandler.ShowTransferNotification(
                "TransferNotificationReceiving/Title".GetLocalizedResource(),
                $"Receiving {totalFiles} files",
                $"{totalFiles} files",
                notificationSequence++,
                0);

            var certificate = await CertificateHelper.GetOrCreateCertificateAsync();
            var context = new SslContext(
                SslProtocols.Tls12 | SslProtocols.Tls13,
                certificate,
                (sender, cert, chain, errors) => true
            );

            client = new Client(context, serverInfo.IpAddress, serverInfo.Port, this, logger);
            if (!client.ConnectAsync())
            {
                throw new IOException("Failed to connect to file transfer server");
            }
            logger.Info($"Connected to file transfer server at {serverInfo.IpAddress}:{serverInfo.Port}");

            // Adding a small delay for the android to open a read channel
            await Task.Delay(500);
            var passwordBytes = Encoding.UTF8.GetBytes(serverInfo.Password + "\n");
            client?.SendAsync(passwordBytes);

            // Process each file in the bulk transfer
            foreach (var fileMetadata in bulkFile.Files)
            {
                try
                {
                    logger.Info($"Starting to receive file {receivedFiles + 1}/{totalFiles}: {fileMetadata.FileName}");

                    // Wait for any existing transfer to complete
                    if (receiveTransferCompletionSource?.Task is not null && !receiveTransferCompletionSource.Task.IsCompleted)
                    {
                        await receiveTransferCompletionSource.Task;
                    }
                    string fullPath = Path.Combine(storageLocation, fileMetadata.FileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                    receiveTransferCompletionSource = new TaskCompletionSource<bool>();
                    currentFileMetadata = fileMetadata;

                    // Open file stream
                    currentFileStream = new FileStream(
                        fullPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920); // 80KB buffer

                    // Wait for this file transfer to complete
                    await receiveTransferCompletionSource.Task;
                    receivedFiles++;

                    logger.Info($"Successfully received file {receivedFiles}/{totalFiles}: {fileMetadata.FileName}");

                    // Update bulk transfer progress
                    var overallProgress = (double)receivedFiles / totalFiles * 100;
                    await notificationHandler.ShowTransferNotification(
                        "TransferNotificationReceiving/Title".GetLocalizedResource(),
                        $"Receiving {totalFiles} files ({receivedFiles}/{totalFiles})",
                        $"{totalFiles} files",
                        notificationSequence,
                        overallProgress);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error receiving file {fileMetadata.FileName}", ex);
                    failedFiles.Add(fileMetadata.FileName);
                    
                    // Clean up the failed file
                    var failedFilePath = Path.Combine(storageLocation, fileMetadata.FileName);
                    if (File.Exists(failedFilePath))
                    {
                        try
                        {
                            File.Delete(failedFilePath);
                        }
                        catch (Exception deleteEx)
                        {
                            logger.Error($"Failed to delete incomplete file {failedFilePath}", deleteEx);
                        }
                    }
                }
            }

            // Show final notification
            if (failedFiles.Count == 0)
            {
                await notificationHandler.ShowTransferNotification(
                    "TransferNotificationReceived/Title".GetLocalizedResource(),
                    $"Successfully received all {totalFiles} files",
                    $"{totalFiles} files",
                    notificationSequence++,
                    null);
                logger.Info($"Bulk file transfer completed successfully: {receivedFiles}/{totalFiles} files received");
            }
            else
            {
                await notificationHandler.ShowTransferNotification(
                    "TransferNotification/Title".GetLocalizedResource(),
                    $"Received {receivedFiles}/{totalFiles} files. {failedFiles.Count} files failed.",
                    $"{totalFiles} files",
                    notificationSequence++,
                    null);
                logger.Warn($"Bulk file transfer completed with errors: {receivedFiles}/{totalFiles} files received, {failedFiles.Count} failed");
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error during bulk file transfer setup", ex);
            await notificationHandler.ShowTransferNotification(
                "TransferNotification/Title".GetLocalizedResource(),
                "Bulk file transfer failed",
                "Bulk transfer",
                notificationSequence++,
                null);
        }
        finally
        {
            CleanupTransfer();
        }
    }

    public async Task ReceiveFile(FileTransfer data, PairedDevice device)
    {
        try
        {
            // Wait for any existing transfer to complete
            if (receiveTransferCompletionSource?.Task is not null)
            {
                await receiveTransferCompletionSource.Task;
            }

            ArgumentNullException.ThrowIfNull(data);

            storageLocation = userSettingsService.GeneralSettingsService.ReceivedFilesPath;
            string fullPath = Path.Combine(storageLocation, data.FileMetadata.FileName);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            receiveTransferCompletionSource = new TaskCompletionSource<bool>();
            var serverInfo = data.ServerInfo;
            currentFileMetadata = data.FileMetadata;

            // Open file stream
            currentFileStream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920); // 80KB buffer

            var certificate = await CertificateHelper.GetOrCreateCertificateAsync();

            var context = new SslContext(
                SslProtocols.Tls12 | SslProtocols.Tls13,
                certificate,
                (sender, cert, chain, errors) => true
            )
            {
                ClientCertificateRequired = false,
            };

            client = new Client(context, serverInfo.IpAddress, serverInfo.Port, this, logger);
            if (!client.ConnectAsync())
            {
                throw new IOException("Failed to connect to file transfer server");
            }
            logger.Info($"Connected to file transfer server at {serverInfo.IpAddress}:{serverInfo.Port}");

            await notificationHandler.ShowTransferNotification(
                "TransferNotificationReceiving/Title".GetLocalizedResource(), 
                $"Receiving {currentFileMetadata.FileName}", 
                currentFileMetadata.FileName, 
                notificationSequence++, 
                0);

            // Adding a small delay for the android to open a read channel
            await Task.Delay(500);
            var passwordBytes = Encoding.UTF8.GetBytes(serverInfo.Password + "\n");
            client?.SendAsync(passwordBytes);

            // Wait for transfer completion
            await receiveTransferCompletionSource.Task;
            if (device.DeviceSettings.ClipboardFilesEnabled)
            {
                var file = await StorageFile.GetFileFromPathAsync(fullPath);
                FileReceived?.Invoke(this, (device, file));
            }

            await notificationHandler.ShowTransferNotification(
                "TransferNotificationReceived/Title".GetLocalizedResource(), 
                $"{currentFileMetadata.FileName} has been saved successfully", 
                fullPath, 
                notificationSequence++, 
                null);
        }
        catch (Exception ex)
        {
            logger.Error("Error during file transfer setup", ex);
        }
        finally
        {
            CleanupTransfer();
        }
    }

    #region Server Events
    public void OnConnected()
    {
        logger.Info("Connected to file transfer server");
        bytesReceived = 0;
    }

    public void OnDisconnected()
    {
        logger.Info("Disconnected from file transfer server");

        // if transfer is not complete
        if (currentFileMetadata != null &&
            currentFileStream != null &&
            bytesReceived < currentFileMetadata.FileSize)
        {
            receiveTransferCompletionSource?.TrySetException(new IOException("Connection to server lost"));
            CleanupTransfer();
        }
    }

    public void OnError(SocketError error)
    {
        logger.Error($"Socket error occurred during file transfer: {error}");
        receiveTransferCompletionSource?.TrySetException(new IOException($"Socket error: {error}"));
        CleanupTransfer();
    }

    public async void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            // Early exit if we don't have valid state
            if (currentFileStream == null || currentFileMetadata == null)
            {
                logger.Debug("Metadata is null");
                return;
            }

            // Write received data to file
            currentFileStream.Write(buffer, (int)offset, (int)size);
            bytesReceived += size;

            // Check if transfer is complete immediately after writing
            if (bytesReceived >= currentFileMetadata.FileSize)
            {
                // Send acknowledgment to the server (add '\n' to ensure proper line ending)
                client?.Send(Encoding.UTF8.GetBytes(COMPLETE_MESSAGE + "\n"));

                // Signal completion before cleanup
                receiveTransferCompletionSource?.TrySetResult(true);
                bytesReceived = 0;

                return; // Exit after handling completion
            }

            // Only proceed with progress update if not complete
            var progress = (double)bytesReceived / currentFileMetadata.FileSize * 100;
            if (Math.Floor(progress) > Math.Floor((double)(bytesReceived - size) / currentFileMetadata.FileSize * 100))
            {
                await notificationHandler.ShowTransferNotification(
                    "TransferNotificationReceiving/Title".GetLocalizedResource(),
                    $"Receiving {currentFileMetadata.FileName}",
                    currentFileMetadata.FileName,
                    notificationSequence,
                    progress);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error processing received file data", ex);
            await notificationHandler.ShowTransferNotification(
                 "TransferNotification/Title".GetLocalizedResource(),
                string.Format("TransferNotificationReceivingError".GetLocalizedResource(), currentFileMetadata?.FileName),
                currentFileMetadata?.FileName ?? "",
                notificationSequence++,
                null);
            CleanupTransfer();
            if (receiveTransferCompletionSource?.Task.IsCompleted == false)
            {
                receiveTransferCompletionSource.TrySetException(ex);
            }
        }
    }
    #endregion
    #endregion

    private void CleanupTransfer()
    {
        try
        {
            if (currentFileStream != null)
            {
                currentFileStream.Close();
                currentFileStream.Dispose();
                currentFileStream = null;
            }

            client?.DisconnectAsync();
            client?.Dispose();
            client = null;

            // Only try to complete the task if it hasn't been completed yet
            if (receiveTransferCompletionSource?.Task.IsCompleted == false)
            {
                receiveTransferCompletionSource.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error during transfer cleanup", ex);
        }
        finally
        {
            currentFileMetadata = null;
            bytesReceived = 0;
        }
    }

    #region Send

    public async void SendFiles(IReadOnlyList<IStorageItem> storageItems)
    {
        try
        {
            var files = storageItems.OfType<StorageFile>().ToArray();
            var devices = deviceManager.PairedDevices.Where(d => d.ConnectionStatus).ToList();
            PairedDevice? selectedDevice = null;
            if (devices.Count == 0)
            {
                return;
            }
            else if (devices.Count == 1)
            {
                selectedDevice = devices[0];
            }
            else if (devices.Count > 1)
            {
                App.MainWindow.AppWindow.Show();
                App.MainWindow.Activate();
                selectedDevice = await DeviceSelector.ShowDeviceSelectionDialog(devices);
            }

            if (selectedDevice == null || selectedDevice.Session == null) return;

            await Task.Run(async () =>
            {
                if (files.Length > 1)
                {
                    await SendBulkFiles(files, selectedDevice);
                }
                else if (files.Length == 1)
                {
                    var file = files[0];
                    var metadata = await file.ToFileMetadata();
                    if (metadata == null) return;

                    await SendFileWithStream(await file.OpenStreamForReadAsync(), metadata, selectedDevice);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error($"Error in sending files: {ex.Message}", ex);
        }
    }

    public async Task SendFileWithStream(Stream stream, FileMetadata metadata, PairedDevice device, bool isClipboard = false)
    {
        try
        {
            // Wait for any existing transfer to complete
            if (sendTransferCompletionSource?.Task.IsCompleted == false)
            {
                await sendTransferCompletionSource.Task;
            }

            server?.Stop();
            session?.Disconnect();
            session = null;

            sendTransferCompletionSource = null;
            connectionSource = null;

            var serverInfo = await InitializeServer();
            var transfer = new FileTransfer
            {
                TransferType = isClipboard ? FileTransferType.Clipboard : FileTransferType.File,
                ServerInfo = serverInfo,
                FileMetadata = metadata
            };

            var json = SocketMessageSerializer.Serialize(transfer);
            logger.Debug($"Sending metadata: {json}");
            sessionManager.SendMessage(device.Session!, json);

            sendTransferCompletionSource = new TaskCompletionSource<bool>();
            await SendFileData(metadata, stream);
            await sendTransferCompletionSource.Task;
        }
        catch (Exception ex)
        {
            logger.Error("Error sending stream data", ex);
            await notificationHandler.ShowTransferNotification(
                "TransferNotification/Title".GetLocalizedResource(),
                $"Error sending {metadata.FileName}: {ex.Message}",
                metadata.FileName,
                notificationSequence++,
                null);
        }
    }

    public async Task SendBulkFiles(StorageFile[] files, PairedDevice device)
    {
        try
        {
            if (files.Length == 1)
            {
                var metadata = await files[0].ToFileMetadata();
                if (metadata == null)
                {
                    return;
                }

                await SendFileWithStream(await files[0].OpenStreamForReadAsync(), metadata, device);
                return;
            }

            var fileMetadataTasks = files.Select(file => file.ToFileMetadata());

            var fileMetadataList = await Task.WhenAll(fileMetadataTasks);

            serverInfo = await InitializeServer();

            var transfer = new BulkFileTransfer
            {
                ServerInfo = serverInfo,
                Files = [.. fileMetadataList]
            };

            // Send metadata first
            sessionManager.SendMessage(device.Session!, SocketMessageSerializer.Serialize(transfer));

            foreach (var file in files)
            {
                var metadata = await file.ToFileMetadata();
                logger.Debug($"Sending file: {metadata.FileName}");

                sendTransferCompletionSource = new TaskCompletionSource<bool>();

                await SendFileData(metadata, File.OpenRead(file.Path));

                // Wait for the transfer to complete before moving to next file
                await sendTransferCompletionSource.Task;
            }

            logger.Debug("All files transferred successfully");
        }
        catch (Exception ex)
        {
            logger.Error("Error in SendBulkFiles", ex);
            throw;
        }
    }

    public async Task SendFileData(FileMetadata metadata, Stream stream)
    {
        try
        {
            if (session == null)
            {
                connectionSource = new TaskCompletionSource<ServerSession>();

                // Wait for Authentication from onReceived event to trigger
                session = await connectionSource.Task;
            }

            const int ChunkSize = 524288; // 512KB

            using (stream)
            {
                var buffer = new byte[ChunkSize];
                long totalBytesRead = 0;

                while (totalBytesRead < metadata.FileSize && session?.IsConnected == true)
                {
                    int bytesRead = await stream.ReadAsync(buffer);
                    if (bytesRead == 0) break;

                    session.Send(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                }
            }

            logger.Info($"Completed file transfer for {metadata.FileName}");
        }
        catch (Exception ex)
        {
            logger.Error("Error in SendFileData", ex);
            sendTransferCompletionSource?.TrySetException(ex);
            throw;
        }
    }

    public async Task<ServerInfo> InitializeServer()
    {
        // Reuse server if it exists
        if (server != null && serverInfo != null)
        {
            return serverInfo;
        }

        var certificate = await CertificateHelper.GetOrCreateCertificateAsync();
        var context = new SslContext(SslProtocols.Tls12 | SslProtocols.Tls13, certificate, (sender, cert, chain, errors) => true)
        {
            ClientCertificateRequired = false,
        };

        // Try each port in the range
        foreach (int port in PORT_RANGE)
        {
            try
            {
                server = new Server(context, IPAddress.Any, port, this, logger)
                {
                    OptionDualMode = true,
                    OptionReuseAddress = true
                };
                server.Start();

                // Server started successfully
                serverInfo = new ServerInfo
                {
                    Port = port,
                    Password = EcdhHelper.GenerateRandomPassword()
                };

                logger.Info($"File transfer server initialized at {serverInfo.IpAddress}:{serverInfo.Port}");
                return serverInfo;
            }
            catch (Exception ex)
            {
                // Log the error and try the next port
                logger.Debug($"Failed to start server on port {port}: {ex.Message}");

                // Clean up failed server instance
                server?.Dispose();
                server = null;
            }
        }
        throw new IOException("Failed to start file transfer server: all ports in range are unavailable");
    }

    private void CleanupServer()
    {
        server?.Stop();
        server?.Dispose();
        server = null;
        serverInfo = null;
        connectionSource = null;
        session = null;
    }

    #region Server Events
    public void OnConnected(ServerSession session)
    {
        logger.Info($"Client connected to file transfer server: {session.Id}");
    }

    public void OnDisconnected(ServerSession session)
    {
        if (sendTransferCompletionSource?.Task.IsCompleted == false)
        {
            sendTransferCompletionSource?.TrySetException(new Exception("Client disconnected"));
        }
        logger.Info($"Client disconnected from file transfer server: {session.Id}");
        CleanupServer();
    }

    public void OnReceived(ServerSession session, byte[] buffer, long offset, long size)
    {
        string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
        logger.Info($"Received message from client: {message}, server password: {serverInfo?.Password}");
        if (connectionSource?.Task.IsCompleted == false && message == serverInfo?.Password)
        {
            connectionSource.SetResult(session);
        }
        if (message == COMPLETE_MESSAGE)
        {
            logger.Info($"Transfer completed");
            sendTransferCompletionSource?.TrySetResult(true);
        }
    }
    #endregion

    #endregion
}
