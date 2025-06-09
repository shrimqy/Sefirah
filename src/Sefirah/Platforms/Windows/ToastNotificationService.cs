using Microsoft.Windows.AppNotifications;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Services;
using Windows.System;

namespace Sefirah.Platforms.Windows;
public class ToastNotificationService(ILogger<ToastNotificationService> logger, INotificationService notificationService, IDeviceManager deviceManager)
{
    public async Task RegisterNotificationAsync()
    {
        try
        {
            // Check if AppNotificationManager is available
            if (!AppNotificationManager.IsSupported())
            {
                logger.LogWarning("App notifications are not supported on this system");
                return;
            }

            // Unregister first to avoid duplicate registrations
            AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;

            // Register for notifications
            await Task.Run(() => AppNotificationManager.Default.Register());
            logger.LogDebug("Successfully registered for toast notifications");
        }
        catch (System.Runtime.InteropServices.COMException comEx) when (comEx.HResult == unchecked((int)0x80040154))
        {
            logger.LogWarning("COM server not registered for notifications. This is expected during development or first run.");
        }
        catch (System.Runtime.InteropServices.COMException comEx) when (comEx.HResult == unchecked((int)0x80070490))
        {
            logger.LogWarning("Element not found during notification registration. App may not be fully initialized yet.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not register for notifications, continuing without notifications");
        }
    }

    private async void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            logger.LogDebug("Notification invoked - Arguments: {Arguments}", string.Join(", ", args.Arguments.Select(x => $"{x.Key}={x.Value}")));

            // Common cleanup for all notifications
            try
            {
                await sender.RemoveByGroupAsync(Constants.Notification.NotificationGroup);
                await Task.Delay(100); // Small delay to ensure removal
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove notifications");
            }

            // Determine notification type first
            if (!args.Arguments.TryGetValue("notificationType", out var notificationType))
            {
                logger.LogWarning("Notification missing type identifier");
                return;
            }

            // Route to appropriate handler
            switch (notificationType)
            {
                case ToastNotificationType.FileTransfer:
                    await HandleFileTransferNotification(args);
                    break;
                
                case ToastNotificationType.RemoteNotification:
                    await HandleMessageNotification(args);
                    break;

                case ToastNotificationType.Clipboard:
                    HandleClipboardNotification(args);
                    break;
                
                default:
                    logger.LogWarning("Unhandled notification type: {NotificationType}", notificationType);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling notification action");
        }
    }

    private async void HandleClipboardNotification(AppNotificationActivatedEventArgs args)
    {
        Uri.TryCreate(args.Arguments["uri"], UriKind.Absolute, out Uri? uri);
        if (uri != null && ClipboardService.IsValidWebUrl(uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private async Task HandleFileTransferNotification(AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments.TryGetValue("action", out string? action))
        {
            switch (action)
            {
                case "openFile":
                    if (args.Arguments.TryGetValue("filePath", out string? filePath) && File.Exists(filePath))
                    {
                        logger.LogDebug("Opening file: {FilePath}", filePath);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        logger.LogWarning("File not found or path not provided - FilePath: {FilePath}", filePath);
                    }
                    break;

                case "openFolder":
                    if (args.Arguments.TryGetValue("folderPath", out string? folderPath) && Directory.Exists(folderPath))
                    {
                        logger.LogDebug("Opening folder: {FolderPath}", folderPath);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{folderPath}\"",
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        logger.LogWarning("Folder not found or path not provided - FolderPath: {FolderPath}", folderPath);
                    }
                    break;
            }
        }
    }

    private async Task HandleMessageNotification(AppNotificationActivatedEventArgs args)
    {
        if (!args.Arguments.TryGetValue("action", out var actionType))
            return;
        
        var notificationKey = args.Arguments["notificationKey"];
        
        // Get deviceId directly from arguments
        if (!args.Arguments.TryGetValue("deviceId", out var deviceId) || string.IsNullOrEmpty(deviceId))
        {
            logger.LogWarning("Notification missing deviceId argument");
            return;
        }

        // Find the device by ID
        var device = deviceManager.FindDeviceById(deviceId);
        if (device == null)
        {
            logger.LogWarning("Could not find device with ID: {DeviceId}", deviceId);
            return;
        }

        switch (actionType)
        {
            case "Reply" when args.UserInput.TryGetValue("textBox", out var replyText):
                notificationService.ProcessReplyAction(device, notificationKey, args.Arguments["replyResultKey"], replyText);
                break;
            case "Click":
                var actionIndex = int.Parse(args.Arguments["actionIndex"]);
                notificationService.ProcessClickAction(device, notificationKey, actionIndex);
                break;
        }
    }
}
