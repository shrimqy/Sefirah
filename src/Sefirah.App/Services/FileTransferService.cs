using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using NetCoreServer;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;
using Sefirah.App.Extensions;
using Sefirah.App.Services.Socket;
using Sefirah.App.Utils;
using Sefirah.App.Utils.Serialization;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;
using static Sefirah.App.Services.ToastNotificationService;
using Server = Sefirah.App.Services.Socket.Server;

namespace Sefirah.App.Services;
public class FileTransferService(
    ILogger logger, 
    ISessionManager sessionManager, 
    IUserSettingsService userSettingsService
    ) : IFileTransferService, ITcpClientProvider, ITcpServerProvider
{
    private readonly string storageLocation = userSettingsService.FeatureSettingsService.ReceivedFilesPath;
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
    
    public event EventHandler<StorageFile>? FileReceived;

    private readonly IEnumerable<int> PORT_RANGE = Enumerable.Range(5152, 18);

    public async Task ReceiveBulkFiles(BulkFileTransfer bulkFile)
    {
        // TODO : Implement bulk file transfer
    }

    public async Task ReceiveFile(FileTransfer data)
    {
        try
        {
            // Wait for any existing transfer to complete
            if (receiveTransferCompletionSource?.Task is not null)
            {
                await receiveTransferCompletionSource.Task;
            }

            ArgumentNullException.ThrowIfNull(data);
            
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

            await ShowTransferNotification("TransferNotificationReceiving/Title".GetLocalizedResource(), $"{currentFileMetadata.FileName}", 0);

            // Adding a small delay for the android to open a read channel
            await Task.Delay(500);
            var passwordBytes = Encoding.UTF8.GetBytes(serverInfo.Password + "\n");
            client?.SendAsync(passwordBytes);

            // Wait for transfer completion
            await receiveTransferCompletionSource.Task;
            if (userSettingsService.FeatureSettingsService.ClipboardFilesEnabled)
            {
                var file = await StorageFile.GetFileFromPathAsync(fullPath);
                FileReceived?.Invoke(this, file);
            }

            await ShowTransferNotification("TransferNotificationReceived/Title".GetLocalizedResource(), $"{currentFileMetadata.FileName} has been saved successfully");
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
                await ShowTransferNotification(
                    "TransferNotificationReceiving/Title".GetLocalizedResource(),
                    $"{currentFileMetadata.FileName}",
                    progress,
                    isReceiving: true);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error processing received file data", ex);
            await ShowTransferNotification(
                 "TransferNotification/Title".GetLocalizedResource(),
                string.Format("TransferNotificationReceivingError".GetLocalizedResource(), currentFileMetadata?.FileName),
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
                
                if (files.Length > 1)
                {
                    await SendBulkFiles(files);
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

                    await SendFile(File.OpenRead(file.Path), metadata);
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

    public async Task SendFile(Stream stream, FileMetadata metadata)
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
            sessionManager.SendMessage(json);

            sendTransferCompletionSource = new TaskCompletionSource<bool>();
            await SendFileData(metadata, stream);
            await sendTransferCompletionSource.Task;
        }
        catch (Exception ex)
        {
            logger.Error("Error sending stream data", ex);
            throw;
        }
    }

    public async Task SendBulkFiles(StorageFile[] files)
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
            sessionManager.SendMessage(SocketMessageSerializer.Serialize(transfer));

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

            const int ChunkSize = 81920 * 2;

            using (stream)
            {
                var buffer = new byte[ChunkSize];
                long totalBytesRead = 0;

                while (totalBytesRead < metadata.FileSize && session?.IsConnected == true)
                {
                    int bytesRead = await stream.ReadAsync(buffer);
                    if (bytesRead == 0) break;

                    session.SendAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    var progress = (double)totalBytesRead / metadata.FileSize * 100;
                }
            }
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
            connectionSource.TrySetResult(session);
        }
        if (message == "Success")
        {
            sendTransferCompletionSource?.TrySetResult(true);
        }
    }

    private async Task ShowTransferNotification(string title, string message, double? progress = null, bool isReceiving = true, bool silent = false)
    {
        string tag = isReceiving ? "file-receive" : "file-send";
        try
        {
            if (progress.HasValue && progress > 0 && progress < 100)
            {
                // Update existing notification with progress
                var progressData = new AppNotificationProgressData(notificationSequence++)
                {
                    Title = title,
                    Value = progress.Value / 100,
                    ValueStringOverride = $"{progress.Value:F0}%",
                    Status = message
                };

                await AppNotificationManager.Default.UpdateAsync(progressData, tag, Constants.Notification.NotificationGroup);
            }
            else
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message)
                    .SetTag(tag)
                    .SetGroup(Constants.Notification.NotificationGroup);

                // Only add progress bar for initial notification
                if (progress == 0)
                {
                    builder.AddProgressBar(new AppNotificationProgressBar()
                        .BindTitle()
                        .BindValue()
                        .BindValueStringOverride()
                        .BindStatus());
                }

                // Add action buttons for completion notification
                if (progress == null && !string.IsNullOrEmpty(currentFileMetadata?.FileName))
                {
                    var filePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Downloads",
                        currentFileMetadata.FileName
                    );

                    builder
                        .AddButton(new AppNotificationButton("TransferNotificationActionOpenFile".GetLocalizedResource())
                            .AddArgument("notificationType", ToastNotificationType.FileTransfer)
                            .AddArgument("action", "openFile")
                            .AddArgument("filePath", filePath))
                        .AddButton(new AppNotificationButton("TransferNotificationActionOpenFolder".GetLocalizedResource())
                            .AddArgument("notificationType", ToastNotificationType.FileTransfer)
                            .AddArgument("action", "openFolder")
                            .AddArgument("folderPath", Path.GetDirectoryName(filePath)));
                }

                if (silent)
                {
                    builder.MuteAudio();
                }

                var notification = builder.BuildNotification();

                // Set initial progress data only for initial notification
                if (progress == 0)
                {
                    var initialProgress = new AppNotificationProgressData(notificationSequence)
                    {
                        Title = title,
                        Value = 0,
                        ValueStringOverride = "0%",
                        Status = message
                    };

                    notification.Progress = initialProgress;
                }

                AppNotificationManager.Default.Show(notification);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Notification failed - Tag: {tag}, Progress: {progress}, Sequence: {notificationSequence}", ex);
        }
    }
}
