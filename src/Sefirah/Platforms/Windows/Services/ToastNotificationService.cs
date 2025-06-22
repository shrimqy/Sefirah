using Microsoft.Windows.AppNotifications;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Services;
using Windows.System;

namespace Sefirah.Platforms.Windows.Services;
public class ToastNotificationService(ILogger logger, IDeviceManager deviceManager)
{
    public async Task RegisterNotificationAsync()
    {
        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;

        try
        {
            await Task.Run(() => AppNotificationManager.Default.Register());
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not register for notifications, continuing without notifications {ex}", ex);
        }
    }

    private async void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            logger.LogDebug("Notification invoked - Arguments: {Arguments}", string.Join(", ", args.Arguments.Select(x => $"{x.Key}={x.Value}")));

            // doesnt work ig? 
            try
            {
                await sender.RemoveByGroupAsync(Constants.Notification.NotificationGroup);
                await Task.Delay(100);
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

    private static async void HandleClipboardNotification(AppNotificationActivatedEventArgs args)
    {
        Uri.TryCreate(args.Arguments["uri"], UriKind.Absolute, out Uri? uri);
        if (uri != null && ClipboardService.IsValidWebUrl(uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private static async Task HandleFileTransferNotification(AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments.TryGetValue("action", out string? action))
        {
            switch (action)
            {
                case "openFile":
                    if (args.Arguments.TryGetValue("filePath", out string? filePath) && File.Exists(filePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    break;

                case "openFolder":
                    if (args.Arguments.TryGetValue("folderPath", out string? folderPath) && Directory.Exists(folderPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{folderPath}\"",
                            UseShellExecute = true
                        });
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
