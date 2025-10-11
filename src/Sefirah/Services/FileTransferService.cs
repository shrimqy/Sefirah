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
    private long bytesTransferred = 0;
    private Client? client;
    private Server? server;
    private ServerInfo? serverInfo;
    private ServerSession? session;
    private uint notificationSequence = 1;
    private TransferContext? currentTransfer;
    private CancellationTokenSource? cancellationTokenSource;

    private const string COMPLETE_MESSAGE = "Complete";

    private TaskCompletionSource<ServerSession>? connectionSource;
    private TaskCompletionSource<bool>? transferCompletionSource;

    public event EventHandler<(PairedDevice device, StorageFile data)>? FileReceived;

    private readonly IEnumerable<int> PORT_RANGE = Enumerable.Range(5152, 18);

    public void CancelTransfer()
    {
        cancellationTokenSource?.Cancel();
    }

    #region Receive
    public async Task ReceiveBulkFiles(BulkFileTransfer bulkFile, PairedDevice device)
    {
        try
        {
            if (transferCompletionSource?.Task is not null)
            {
                await transferCompletionSource.Task;
            }

            storageLocation = userSettingsService.GeneralSettingsService.ReceivedFilesPath;
            var serverInfo = bulkFile.ServerInfo;

            currentTransfer = new TransferContext(
                device.Name,
                $"transfer_{DateTime.Now.Ticks}",
                bulkFile.Files
            );

            cancellationTokenSource = new CancellationTokenSource();

            var certificate = await CertificateHelper.GetOrCreateCertificateAsync();
            var context = new SslContext(
                SslProtocols.Tls12 | SslProtocols.Tls13,
                certificate,
                (sender, cert, chain, errors) => true
            );

            client = new Client(context, serverInfo.IpAddress, serverInfo.Port, this);

            if (!client.ConnectAsync()) 
                throw new IOException("Failed to connect to file transfer server");

            // Adding a small delay for the android to open a read channel
            await Task.Delay(500);
            var passwordBytes = Encoding.UTF8.GetBytes(serverInfo.Password + "\n");
            client?.SendAsync(passwordBytes);

            notificationHandler.ShowFileTransferNotification(
                string.Format("FileTransferNotification.ReceivingBulk".GetLocalizedResource(), 1, currentTransfer.Files.Count, currentTransfer.Device),
                currentTransfer.Files[0].FileName,
                currentTransfer.TransferId,
                notificationSequence++);

            foreach (var fileMetadata in bulkFile.Files)
            {
                try
                {
                    // Check if the entire bulk transfer has been cancelled
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    logger.Info($"Starting to receive file {currentTransfer.CurrentFileIndex + 1}/{bulkFile.Files.Count}: {fileMetadata.FileName}");

                    if (transferCompletionSource?.Task is not null && !transferCompletionSource.Task.IsCompleted)
                    {
                        await transferCompletionSource.Task;
                    }
                    string fullPath = Path.Combine(storageLocation, fileMetadata.FileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                    transferCompletionSource = new();
                    currentFileMetadata = fileMetadata;
                    currentFileStream = new FileStream(fullPath, FileMode.Create);

                    // Wait for this file transfer to complete
                    await transferCompletionSource.Task;

                    currentTransfer.CurrentFileIndex++;
                    logger.Info($"Received file {fileMetadata.FileName}");

                    // Clean up the file stream for the previous file
                    CleanupFileStream();
                }
                catch (Exception ex)
                {
                    CleanupFileStream();
                    var failedFilePath = Path.Combine(storageLocation, fileMetadata.FileName);
                    if (File.Exists(failedFilePath))
                    {
                        File.Delete(failedFilePath);
                    }

                    if (ex is OperationCanceledException)
                    {
                        logger.Info("Bulk file transfer cancelled by user");
                        throw;
                    }
                    logger.Error($"Error receiving file {fileMetadata.FileName}", ex);
                }
            }

            notificationHandler.ShowCompletedFileTransferNotification(
                string.Format("FileTransferNotification.CompletedBulk".GetLocalizedResource(), currentTransfer.Files.Count, currentTransfer.Device),
                currentTransfer.TransferId,
                folderPath: storageLocation);

            logger.Warn($"Bulk file transfer completed with errors: {currentTransfer.CurrentFileIndex}/{currentTransfer.Files.Count} files received failed");
        }
        catch (Exception ex)
        {
            logger.Error("Error during bulk file transfer setup", ex);
        }
        finally
        {
            CleanupClient();
        }
    }

    private void CleanupFileStream()
    {
        currentFileStream?.Close();
        currentFileStream?.Dispose();
        currentFileStream = null;
        currentFileMetadata = null;
    }

    public async Task ReceiveFile(FileTransfer data, PairedDevice device)
    {
        storageLocation = userSettingsService.GeneralSettingsService.ReceivedFilesPath;
        string fullPath = Path.Combine(storageLocation, data.FileMetadata.FileName);
        try
        {
            // Wait for any existing transfer to complete
            if (transferCompletionSource?.Task is not null)
            {
                await transferCompletionSource.Task;
            }
            
            // Create transfer context for single file
            currentTransfer = new TransferContext(
                device.Name,
                $"transfer_{DateTime.Now.Ticks}",
                [data.FileMetadata]
            );

            transferCompletionSource = new TaskCompletionSource<bool>();
            cancellationTokenSource = new CancellationTokenSource();
            var serverInfo = data.ServerInfo;
            currentFileMetadata = data.FileMetadata;

            currentFileStream = new FileStream(fullPath, FileMode.Create);

            var context = new SslContext(
                SslProtocols.Tls12 | SslProtocols.Tls13,
                await CertificateHelper.GetOrCreateCertificateAsync(),
                (sender, cert, chain, errors) => true
            )
            {
                ClientCertificateRequired = false,
            };

            client = new Client(context, serverInfo.IpAddress, serverInfo.Port, this);
            if (!client.ConnectAsync())
                throw new IOException("Failed to connect to file transfer server");

            notificationHandler.ShowFileTransferNotification(
                string.Format("FileTransferNotification.Receiving".GetLocalizedResource(), currentTransfer.Device), 
                currentFileMetadata.FileName, 
                currentTransfer.TransferId,
                notificationSequence++, 
                0);

            // Adding a small delay for the android to open a read channel
            await Task.Delay(500);
            var passwordBytes = Encoding.UTF8.GetBytes(serverInfo.Password + "\n");
            client?.SendAsync(passwordBytes);

            await transferCompletionSource.Task;

            if (device.DeviceSettings.ClipboardFilesEnabled)
            {
                var file = await StorageFile.GetFileFromPathAsync(fullPath);
                FileReceived?.Invoke(this, (device, file));
            }

            notificationHandler.ShowCompletedFileTransferNotification(
                string.Format("FileTransferNotification.CompletedSingle".GetLocalizedResource(), currentFileMetadata.FileName, currentTransfer.Device), 
                currentTransfer.TransferId, 
                fullPath);
        }
        catch (Exception ex)
        {
            logger.Error("Error during file transfer setup", ex);
            CleanupFileStream();
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        finally
        {
            CleanupClient();
        }
    }

    #region Server Events

    public void OnConnected()
    {
        logger.Info("Connected to file transfer server");
    }

    public void OnDisconnected()
    {
        logger.Info("Disconnected from file transfer server");

        // if transfer is not complete
        if (currentFileMetadata != null &&
            currentFileStream != null &&
            currentTransfer != null &&
            currentTransfer.BytesTransferred < currentFileMetadata.FileSize)
        {
            transferCompletionSource?.TrySetException(new IOException("Connection to server lost"));
            CleanupClient();
        }
    }

    public void OnError(SocketError error)
    {
        logger.Error($"Socket error occurred during file transfer: {error}");
        transferCompletionSource?.TrySetException(new IOException($"Socket error: {error}"));
        CleanupClient();
    }

    public void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            cancellationTokenSource?.Token.ThrowIfCancellationRequested();

            if (currentFileStream == null || currentFileMetadata == null || currentTransfer == null) return;

            currentFileStream.Write(buffer, (int)offset, (int)size);
            bytesTransferred += size;
            currentTransfer.BytesTransferred += size;

            var progress = (double)currentTransfer.BytesTransferred / currentTransfer.TotalBytes * 100;
            
            notificationHandler.ShowFileTransferNotification(
                string.Format("FileTransferNotification.Receiving".GetLocalizedResource(), currentTransfer.Device),
                currentFileMetadata.FileName,
                currentTransfer.TransferId,
                notificationSequence,
                progress);

            if (bytesTransferred >= currentFileMetadata.FileSize)
            {
                client?.Send(Encoding.UTF8.GetBytes(COMPLETE_MESSAGE + "\n"));
                bytesTransferred = 0;
                transferCompletionSource?.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            transferCompletionSource?.TrySetException(ex);
        }
    }
    #endregion
    #endregion

    private void CleanupClient()
    {
        try
        {
            CleanupFileStream();

            client?.DisconnectAsync();
            client?.Dispose();
            client = null;
            transferCompletionSource = null;
        }
        catch (Exception ex)
        {
            logger.Error("Error during transfer cleanup", ex);
        }
        finally
        {
            currentFileMetadata = null;
            currentTransfer = null;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
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
                    var metadata = await files[0].ToFileMetadata();
                    if (metadata == null) return;

                    await SendFile(files[0], metadata, selectedDevice);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error($"Error in sending files: {ex.Message}", ex);
        }
    }

    public async Task SendFile(StorageFile file, FileMetadata metadata, PairedDevice device, FileTransferType transferType = FileTransferType.File)
    {
        try
        {
            // Wait for any existing transfer to complete
            if (transferCompletionSource?.Task.IsCompleted == false)
            {
                await transferCompletionSource.Task;
            }

            server?.Stop();
            session?.Disconnect();
            session = null;
            connectionSource = null;

            currentTransfer = new TransferContext(
                device.Name,
                $"transfer_{DateTime.Now.Ticks}",
                [metadata]
            );

            cancellationTokenSource = new CancellationTokenSource();

            var serverInfo = await InitializeServer();
            var transfer = new FileTransfer
            {
                TransferType = transferType,
                ServerInfo = serverInfo,
                FileMetadata = metadata
            };

            var json = SocketMessageSerializer.Serialize(transfer);
            logger.Debug($"Sending metadata: {json}");
            sessionManager.SendMessage(device.Session!, json);

            notificationHandler.ShowFileTransferNotification(
                string.Format("FileTransferNotification.Sending".GetLocalizedResource(), currentTransfer.Device),
                metadata.FileName,
                currentTransfer.TransferId,
                notificationSequence++,
                0);

            transferCompletionSource = new();
            await SendFileData(metadata, await file.OpenStreamForReadAsync());
            await transferCompletionSource.Task;

            notificationHandler.ShowCompletedFileTransferNotification(
                string.Format("FileTransferNotification.SentSingle".GetLocalizedResource(), metadata.FileName, currentTransfer.Device),
                currentTransfer.TransferId);
        }
        catch (OperationCanceledException)
        {
            logger.Info("File transfer cancelled by user");
        }
        catch (Exception ex)
        {
            logger.Error("Error sending stream data", ex);
        }
        finally
        {
            CleanupServer();
        }
    }

    public async Task SendBulkFiles(StorageFile[] files, PairedDevice device)
    {
        try
        {
            var fileMetadataList = await Task.WhenAll(files.Select(file => file.ToFileMetadata()));

            currentTransfer = new TransferContext(
                device.Name,
                $"transfer_{DateTime.Now.Ticks}",
                fileMetadataList.ToList()
            );

            cancellationTokenSource = new CancellationTokenSource();

            serverInfo = await InitializeServer();

            var transfer = new BulkFileTransfer
            {
                ServerInfo = serverInfo,
                Files = [.. fileMetadataList]
            };

            // Send metadata first
            sessionManager.SendMessage(device.Session!, SocketMessageSerializer.Serialize(transfer));

            notificationHandler.ShowFileTransferNotification(
                string.Format("FileTransferNotification.SendingBulk".GetLocalizedResource(), 1, currentTransfer.Files.Count, currentTransfer.Device),
                currentTransfer.Files[0].FileName,
                currentTransfer.TransferId,
                notificationSequence++);

            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    logger.Debug($"Sending file: {fileMetadataList[i].FileName}");

                    transferCompletionSource = new TaskCompletionSource<bool>();

                    await SendFileData(fileMetadataList[i], await files[i].OpenStreamForReadAsync());

                    await transferCompletionSource.Task;
                    currentTransfer.CurrentFileIndex++;
                }
                catch (OperationCanceledException)
                {
                    logger.Info("Bulk file transfer cancelled by user");
                    throw;
                }
            }

            notificationHandler.ShowCompletedFileTransferNotification(
                string.Format("FileTransferNotification.SentBulk".GetLocalizedResource(), currentTransfer.Files.Count, currentTransfer.Device),
                currentTransfer.TransferId);

            logger.Debug("All files transferred successfully");
        }
        catch (OperationCanceledException)
        {
            logger.Info("Bulk file transfer cancelled by user");
        }
        catch (Exception ex)
        {
            logger.Error("Error in SendBulkFiles", ex);
        }
        finally
        {
            CleanupServer();
        }
    }

    public async Task SendFileData(FileMetadata metadata, Stream stream)
    {
        try
        {
            if (session == null)
            {
                connectionSource = new();

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
                    // Check if transfer has been canceled
                    cancellationTokenSource?.Token.ThrowIfCancellationRequested();

                    int bytesRead = await stream.ReadAsync(buffer);
                    if (bytesRead == 0) break;

                    session.Send(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (currentTransfer != null)
                    {
                        currentTransfer.BytesTransferred = totalBytesRead;
                        var progress = (double)totalBytesRead / metadata.FileSize * 100;

                        if (currentTransfer.Files.Count > 1)
                        {
                            notificationHandler.ShowFileTransferNotification(
                                string.Format("FileTransferNotification.SendingBulk".GetLocalizedResource(), currentTransfer.CurrentFileIndex + 1, currentTransfer.Files.Count, currentTransfer.Device),
                                metadata.FileName,
                                currentTransfer.TransferId,
                                notificationSequence,
                                progress);
                        }
                        else
                        {
                            notificationHandler.ShowFileTransferNotification(
                                string.Format("FileTransferNotification.Sending".GetLocalizedResource(), currentTransfer.Device),
                                metadata.FileName,
                                currentTransfer.TransferId,
                                notificationSequence,
                                progress);
                        }
                    }
                }
            }

            logger.Info($"Completed file transfer for {metadata.FileName}");
        }
        catch (Exception ex)
        {
            logger.Error("Error in SendFileData", ex);
            throw;
        }
    }

    public async Task<ServerInfo> InitializeServer()
    {
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
                    OptionReuseAddress = true,
                };
                server.Start();

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
                logger.Debug($"Failed to start server on port {port}: {ex.Message}");

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
        currentTransfer = null;
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }

    #region Server Events
    public void OnConnected(ServerSession session)
    {
        logger.Info($"Client connected to file transfer server: {session.Id}");
    }

    public void OnDisconnected(ServerSession session)
    {
        if (transferCompletionSource?.Task.IsCompleted == false)
        {
            transferCompletionSource?.TrySetException(new Exception("Client disconnected"));
        }
        logger.Info($"Client disconnected from file transfer server: {session.Id}");
        CleanupServer();
    }

    public void OnReceived(ServerSession session, byte[] buffer, long offset, long size)
    {
        string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
        if (connectionSource?.Task.IsCompleted == false && message == serverInfo?.Password)
        {
            connectionSource.SetResult(session);
        }
        if (message == COMPLETE_MESSAGE)
        {
            logger.Info($"Transfer completed");
            transferCompletionSource?.TrySetResult(true);
        }
    }

    #endregion

    #endregion
}
