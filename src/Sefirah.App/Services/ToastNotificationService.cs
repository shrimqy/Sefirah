using Microsoft.Windows.AppNotifications;
using Sefirah.App.Data.Contracts;

namespace Sefirah.App.Services;
public class ToastNotificationService(ILogger logger, INotificationService notificationService)
{
    public async void RegisterNotification()
    {
        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;

        try
        {
            await Task.Run(() => AppNotificationManager.Default.Register());
        }
        catch (Exception ex)
        {
            logger.Warn("Could not register for notifications, continuing without notifications", ex);
        }
    }

    private async void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            logger.Debug($"Notification invoked - Arguments: {string.Join(", ", args.Arguments.Select(x => $"{x.Key}={x.Value}"))}");

            // Common cleanup for all notifications
            try
            {
                await sender.RemoveByGroupAsync(Constants.Notification.NotificationGroup);
                await Task.Delay(100); // Small delay to ensure removal
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to remove notifications: {ex.Message}");
            }

            // Determine notification type first
            if (!args.Arguments.TryGetValue("notificationType", out var notificationType))
            {
                logger.Warn("Notification missing type identifier");
                return;
            }

            // Route to appropriate handler
            switch (notificationType)
            {
                case ToastNotificationType.FileTransfer:
                    await HandleFileTransferNotification(args);
                    break;
                
                case ToastNotificationType.RemoteNotification:
                    HandleMessageNotification(args);
                    break;
                
                // Add other notification types here
                
                default:
                    logger.Warn($"Unhandled notification type: {notificationType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling notification action: {ex.Message}", ex);
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
                        logger.Debug($"Opening file: {filePath}");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        logger.Warn($"File not found or path not provided - FilePath: {filePath}");
                    }
                    break;

                case "openFolder":
                    if (args.Arguments.TryGetValue("folderPath", out string? folderPath) && Directory.Exists(folderPath))
                    {
                        logger.Debug($"Opening folder: {folderPath}");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{folderPath}\"",
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        logger.Warn($"Folder not found or path not provided - FolderPath: {folderPath}");
                    }
                    break;
            }
        }
    }

    private void HandleMessageNotification(AppNotificationActivatedEventArgs args)
    {
        if (!args.Arguments.TryGetValue("action", out var actionType))
            return;
        
        var notificationKey = args.Arguments["notificationKey"];
        switch (actionType)
        {
            case "Reply" when args.UserInput.TryGetValue("textBox", out var replyText):
                notificationService.ProcessReplyAction(notificationKey, args.Arguments["replyResultKey"], replyText);
                break;
            case "Click":
                var actionIndex = int.Parse(args.Arguments["actionIndex"]);
                notificationService.ProcessClickAction(notificationKey, actionIndex);
                break;
        }
    }

    public static class ToastNotificationType
    {
        public const string FileTransfer = "FileTransfer";
        public const string RemoteNotification = "remoteNotification";
    }
}
