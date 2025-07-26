using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using CommunityToolkit.WinUI;
using NetCoreServer;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Extensions;
using Sefirah.Helpers;
using Sefirah.Services.Socket;
using Sefirah.Utils.Serialization;
using Uno.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;

namespace Sefirah.Services;
public class FileTransferService(
    ILogger logger,
    ISessionManager sessionManager,
    IUserSettingsService userSettingsService,
    IDeviceManager deviceManager,
    IPlatformNotificationHandler notificationHandler
    ) : IFileTransferService, ITcpClientProvider, ITcpServerProvider
{
    private readonly string? storageLocation;
    private FileStream? currentFileStream;
    private FileMetadata? currentFileMetadata;
    private long bytesReceived;
    private Client? client;
    private Server? server;
    private ServerInfo? serverInfo;
    private ServerSession? session;
    private uint notificationSequence = 1;

    private TaskCompletionSource<ServerSession>? connectionSource;
    private TaskCompletionSource<bool>? sendTransferCompletionSource;
    private TaskCompletionSource<bool>? receiveTransferCompletionSource;

    public event EventHandler<(PairedDevice device, StorageFile data)> FileReceived;

    private readonly IEnumerable<int> PORT_RANGE = Enumerable.Range(5152, 18);

    public async Task ReceiveBulkFiles(BulkFileTransfer bulkFile)
    {
        // TODO : Implement bulk file transfer
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

            var storageLocation = userSettingsService.GeneralSettingsService.ReceivedFilesPath;
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
                SslProtocols.Tls12,
                certificate,
                (sender, cert, chain, errors) => true
            );

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
                0, 
                isReceiving: true);

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
                null, 
                isReceiving: true);
        }
        catch (Exception ex)
        {
            logger.Error("Error during file transfer setup", ex);
            throw;
        }
        finally
        {
            CleanupTransfer(receiveTransferCompletionSource?.Task.IsCompletedSuccessfully == true);
        }
    }

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
            CleanupTransfer(false);
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
                return;
            }

            // Write received data to file
            currentFileStream.Write(buffer, (int)offset, (int)size);
            bytesReceived += size;

            // Check if transfer is complete immediately after writing
            if (bytesReceived >= currentFileMetadata.FileSize)
            {
                // Send acknowledgment to the server
                var successBytes = Encoding.UTF8.GetBytes("Complete");
                client?.SendAsync(successBytes);

                // Signal completion before cleanup
                receiveTransferCompletionSource?.TrySetResult(true);

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
                    progress,
                    isReceiving: true);
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
                null,
                isReceiving: true);
            CleanupTransfer(false);
            if (receiveTransferCompletionSource?.Task.IsCompleted == false)
            {
                receiveTransferCompletionSource.TrySetException(ex);
            }
        }
    }

    private void CleanupTransfer(bool success = false)
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

            if (!success && currentFileMetadata != null)
            {
                // Delete incomplete file
                var filePath = Path.Combine(storageLocation, currentFileMetadata.FileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
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

#if WINDOWS
    // Share Target handler
    public async Task ProcessShareAsync(ShareOperation shareOperation)
    {
        try
        {
            if (shareOperation.Data.Contains(StandardDataFormats.StorageItems))
            {
                var items = await shareOperation.Data.GetStorageItemsAsync();
                // Convert IStorageItem list to StorageFile array
                var files = items.OfType<StorageFile>().ToArray();

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
                    selectedDevice = await ShowDeviceSelectionDialog(devices);
                }

                if (selectedDevice == null || selectedDevice.Session == null) return;

                if (files.Length > 1)
                {
                    await SendBulkFiles(files, selectedDevice);
                }
                else if (files.Length == 1)
                {
                    var file = files[0];
                    var metadata = new FileMetadata
                    {
                        FileName = file.Name,
                        MimeType = file.ContentType,
                        FileSize = (long)(await file.GetBasicPropertiesAsync()).Size,
                        Uri = file.Path
                    };
                    await SendFile(File.OpenRead(file.Path), metadata, selectedDevice);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error in ProcessShareAsync: {ex.Message}", ex);
        }
        finally
        {
            shareOperation.ReportCompleted();
        }
    }
#endif

    private async Task<PairedDevice?> ShowDeviceSelectionDialog(List<PairedDevice> onlineDevices)
    {
        PairedDevice? selectedDevice = null;
        
        await App.MainWindow!.DispatcherQueue!.EnqueueAsync(async () =>
        {
            var deviceOptions = new List<ComboBoxItem>();
            foreach (var device in onlineDevices)
            {
                var displayName = device.Name ?? "Unknown";
                var item = new ComboBoxItem
                {
                    Content = $"{displayName}",
                    Tag = device
                };
                deviceOptions.Add(item);
            }

            var deviceSelector = new ComboBox
            {
                ItemsSource = deviceOptions,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = 0
            };

            var dialog = new ContentDialog
            {
                XamlRoot = App.MainWindow!.Content!.XamlRoot,
                Title = "SelectDevice".GetLocalizedResource(),
                Content = deviceSelector,
                PrimaryButtonText = "Start".GetLocalizedResource(),
                CloseButtonText = "Cancel".GetLocalizedResource(),
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && deviceSelector.SelectedItem is ComboBoxItem selected)
            {
                selectedDevice = selected.Tag as PairedDevice;
            }
        });

        return selectedDevice;
    }

    public async Task SendFile(Stream stream, FileMetadata metadata, PairedDevice device)
    {
        try
        {
            // Wait for any existing transfer to complete
            if (sendTransferCompletionSource?.Task.IsCompleted == false)
            {
                await sendTransferCompletionSource.Task;
            }

            if (server != null)
            {
                server.Stop();
                server.Dispose();
                server = null;
            }

            if (session != null)
            {
                session.Disconnect();
                session = null;
            }

            sendTransferCompletionSource = null;
            connectionSource = null;

            var serverInfo = await InitializeServer();
            var transfer = new FileTransfer
            {
                ServerInfo = serverInfo,
                FileMetadata = metadata
            };

            var json = SocketMessageSerializer.Serialize(transfer);
            logger.Debug($"Sending metadata: {json}");
            sessionManager.SendMessage(device.Session!, json);

            await notificationHandler.ShowTransferNotification(
                "TransferNotificationSending/Title".GetLocalizedResource(),
                $"Sending {metadata.FileName}",
                metadata.FileName,
                notificationSequence++,
                0,
                isReceiving: false);

            sendTransferCompletionSource = new TaskCompletionSource<bool>();
            await SendFileData(metadata, stream);
            await sendTransferCompletionSource.Task;

            await notificationHandler.ShowTransferNotification(
                "TransferNotificationSent/Title".GetLocalizedResource(),
                $"{metadata.FileName} has been sent successfully",
                metadata.FileName,
                notificationSequence++,
                null,
                isReceiving: false);
        }
        catch (Exception ex)
        {
            logger.Error("Error sending stream data", ex);
            await notificationHandler.ShowTransferNotification(
                "TransferNotification/Title".GetLocalizedResource(),
                $"Error sending {metadata.FileName}: {ex.Message}",
                metadata.FileName,
                notificationSequence++,
                null,
                isReceiving: false);
            throw;
        }
    }

    public async Task SendBulkFiles(StorageFile[] files, PairedDevice device)
    {
        var fileMetadataTasks = files.Select(async file => new FileMetadata
        {
            FileName = file.Name,
            MimeType = file.ContentType,
            FileSize = (long)(await file.GetBasicPropertiesAsync()).Size,
            Uri = file.Path
        });

        var fileMetadataList = await Task.WhenAll(fileMetadataTasks);

        try
        {
            serverInfo = await InitializeServer();

            var transfer = new BulkFileTransfer
            {
                ServerInfo = serverInfo,
                Files = [.. fileMetadataList]
            };

            // Send metadata first
            sessionManager.SendMessage(device.Session!, SocketMessageSerializer.Serialize(transfer));

            foreach (var file in fileMetadataList)
            {
                logger.Debug($"Sending file: {file.FileName}");

                sendTransferCompletionSource = new TaskCompletionSource<bool>();

                await SendFileData(file, File.OpenRead(file.Uri!));

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
                double lastReportedProgress = 0;

                while (totalBytesRead < metadata.FileSize && session?.IsConnected == true)
                {
                    int bytesRead = await stream.ReadAsync(buffer);
                    if (bytesRead == 0) break;

                    session.Send(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    var progress = (double)totalBytesRead / metadata.FileSize * 100;
                    if (Math.Floor(progress) > Math.Floor(lastReportedProgress))
                    {
                        await notificationHandler.ShowTransferNotification(
                            "TransferNotificationSending/Title".GetLocalizedResource(),
                            $"Sending {metadata.FileName}",
                            metadata.FileName,
                            notificationSequence,
                            progress,
                            isReceiving: false);
                        lastReportedProgress = progress;
                    }
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
        var context = new SslContext(SslProtocols.Tls12, certificate);

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

    // Server session event handlers
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
        if (message == "Success")
        {
            logger.Info($"Transfer completed");
            sendTransferCompletionSource?.TrySetResult(true);
        }
    }
}
