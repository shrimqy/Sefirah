using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Extensions;
using Sefirah.Services;
using Sefirah.Utils;
using Uno.Logging;
using Windows.System;
using static Sefirah.Constants;

namespace Sefirah.Platforms.Windows.Services;

/// <summary>
/// Windows implementation of the platform notification handler
/// </summary>
public class WindowsNotificationHandler(ILogger logger, ISessionManager sessionManager, IDeviceManager deviceManager) : IPlatformNotificationHandler
{
    /// <inheritdoc />
    public async Task ShowRemoteNotification(NotificationMessage message, string deviceId)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(message.AppName, new AppNotificationTextProperties().SetMaxLines(1))
                .AddText(message.Title)
                .AddText(message.Text)
                .SetTag(message.Tag ?? string.Empty)
                .SetGroup(message.GroupKey ?? string.Empty);

            // Handle icons - check for local app icon first
            if (!string.IsNullOrEmpty(message.LargeIcon))
            {
                await SetNotificationIcon(builder, message.LargeIcon, "largeIcon.png");
            }
            else if (!string.IsNullOrEmpty(message.AppIcon) || !string.IsNullOrEmpty(message.AppPackage))
            {
                await SetAppNotificationIcon(builder, message.AppPackage, message.AppIcon);
            }

            // Handle actions
            foreach (var action in message.Actions)
            {
                if (action == null) continue;

                if (action.IsReplyAction)
                {
                    builder
                        .AddTextBox("textBox", "ReplyPlaceholder".GetLocalizedResource(), "")
                        .AddButton(new AppNotificationButton("SendButton".GetLocalizedResource())
                            .AddArgument("notificationType", ToastNotificationType.RemoteNotification)
                            .AddArgument("notificationKey", message.NotificationKey)
                            .AddArgument("replyResultKey", message.ReplyResultKey)
                            .AddArgument("action", "Reply")
                            .AddArgument("deviceId", deviceId)
                                .SetInputId("textBox"));
                }
                else
                {
                    builder.AddButton(new AppNotificationButton(action.Label)
                        .AddArgument("notificationType", ToastNotificationType.RemoteNotification)
                        .AddArgument("action", "Click")
                        .AddArgument("actionIndex", action.ActionIndex.ToString())
                        .AddArgument("notificationKey", message.NotificationKey)
                        .AddArgument("deviceId", deviceId));
                }
            }

            var notification = builder.BuildNotification();
            notification.ExpiresOnReboot = true;
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show remote notification");
        }
    }

    public async Task ShowTransferNotification(string title, string message, string fileName, uint notificationSequence, double? progress = null, bool silent = false)
    {
        try
        {
            var tag = $"filetransfer_{notificationSequence}";
            if (progress.HasValue && progress > 0 && progress < 100)
            {
                // Update existing notification with progress
                var progressData = new AppNotificationProgressData(notificationSequence)
                {
                    Title = title,
                    Value = progress.Value / 100,
                    ValueStringOverride = $"{progress.Value:F0}%",
                    Status = message
                };

                await AppNotificationManager.Default.UpdateAsync(progressData, tag, Constants.Notification.FileTransferGroup);
            }
            else
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message)
                    .SetTag(tag)
                    .SetGroup(Constants.Notification.FileTransferGroup);

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
                if (progress == null && !string.IsNullOrEmpty(fileName))
                {
                    // For received files, get the default received files location
                    var filePath = fileName; // This will be the full path passed from FileTransferService

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
            logger.Error($"Notification failed, Progress: {progress}, Sequence: {notificationSequence}", ex);
        }
    }

    /// <inheritdoc />
    public async Task ShowClipboardNotification(string title, string text, string? iconPath = null)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(text)
                .SetTag($"simple_{DateTime.Now.Ticks}")
                .SetGroup("simple");

            var notification = builder.BuildNotification();
            notification.ExpiresOnReboot = true;
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show simple notification");
        }
    }

    /// <inheritdoc />
    public async Task ShowClipboardNotificationWithActions(string title, string text, string? actionLabel = null, string? actionData = null)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(text)
                .SetTag($"clipboard_{DateTime.Now.Ticks}")
                .SetGroup("clipboard");

            if (!string.IsNullOrEmpty(actionLabel) && !string.IsNullOrEmpty(actionData))
            {
                builder.AddButton(new AppNotificationButton(actionLabel)
                    .AddArgument("notificationType", ToastNotificationType.Clipboard)
                    .AddArgument("uri", actionData));
            }

            var notification = builder.BuildNotification();
            notification.ExpiresOnReboot = true;
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show clipboard notification");
        }
    }

    /// <inheritdoc />
    public async Task ShowFileTransferNotification(string title, string text, string? filePath = null, string? folderPath = null)
    {
        // TODO: show hero image if available
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(text)
                .SetTag($"filetransfer_{DateTime.Now.Ticks}")
                .SetGroup("filetransfer");

            if (!string.IsNullOrEmpty(filePath))
            {
                builder.AddButton(new AppNotificationButton("Open File")
                    .AddArgument("notificationType", ToastNotificationType.FileTransfer)
                    .AddArgument("action", "openFile")
                    .AddArgument("filePath", filePath));
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                builder.AddButton(new AppNotificationButton("Open Folder")
                    .AddArgument("notificationType", ToastNotificationType.FileTransfer)
                    .AddArgument("action", "openFolder")
                    .AddArgument("folderPath", folderPath));
            }

            var notification = builder.BuildNotification();
            notification.ExpiresOnReboot = true;
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show file transfer notification");
        }
    }

    /// <inheritdoc />
    public async Task RegisterForNotifications()
    {
        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;

        try
        {
            await Task.Run(() => AppNotificationManager.Default.Register());
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
                await sender.RemoveByGroupAsync(Constants.Notification.FileTransferGroup);
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
                    HandleFileTransferNotification(args);
                    break;
                
                case ToastNotificationType.RemoteNotification:
                    HandleMessageNotification(args);
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
        if (args.Arguments.TryGetValue("uri", out var uriString) && Uri.TryCreate(uriString, UriKind.Absolute, out Uri? uri))
        {
            if (ClipboardService.IsValidWebUrl(uri))
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }
    }

    private void HandleFileTransferNotification(AppNotificationActivatedEventArgs args)
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
                    else
                    {
                        logger.LogWarning("File not found or path not provided - FilePath: {FilePath}", filePath);
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
                    else
                    {
                        logger.LogWarning("Folder not found or path not provided - FolderPath: {FolderPath}", folderPath);
                    }
                    break;
            }
        }
    }

    private void HandleMessageNotification(AppNotificationActivatedEventArgs args)
    {
        if (!args.Arguments.TryGetValue("action", out var actionType))
            return;
        
        if (!args.Arguments.TryGetValue("deviceId", out var deviceId))
            return;

        var device = deviceManager.FindDeviceById(deviceId);
        if (device == null) return;

        var notificationKey = args.Arguments["notificationKey"];
        switch (actionType)
        {
            case "Reply" when args.UserInput.TryGetValue("textBox", out var replyText):
                if (args.Arguments.TryGetValue("replyResultKey", out var replyResultKey))
                {
                    NotificationActionUtils.ProcessReplyAction(sessionManager, logger, device, notificationKey, replyResultKey, replyText);
                }
                break;
            case "Click":
                if (args.Arguments.TryGetValue("actionIndex", out var actionIndexStr) && int.TryParse(actionIndexStr, out var actionIndex))
                {
                    NotificationActionUtils.ProcessClickAction(sessionManager, logger, device, notificationKey, actionIndex);
                }
                break;
        }
    }

    /// <inheritdoc />
    public async Task RemoveNotification(string notificationKey)
    {
        try
        {
            await AppNotificationManager.Default.RemoveByTagAsync(notificationKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove notification with key {NotificationKey}", notificationKey);
        }
    }

    /// <inheritdoc />
    public async Task RemoveNotificationsByGroup(string groupKey)
    {
        try
        {
            await AppNotificationManager.Default.RemoveByGroupAsync(groupKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove notifications for group {GroupKey}", groupKey);
        }
    }

    /// <inheritdoc />
    public async Task ClearAllNotifications()
    {
        try
        {
            await AppNotificationManager.Default.RemoveAllAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear all notifications");
        }
    }

    private async Task SetAppNotificationIcon(AppNotificationBuilder builder, string? appPackage, string? appIconBase64)
    {
        try
        {

            if (!string.IsNullOrEmpty(appPackage))
            {
                var iconUri = await ImageUtils.GetAppIconUri($"{appPackage}.png");
                if (iconUri != null)
                {
                    builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);
                    return;
                }
            }

            // Fall back to saving the base64 icon if no local icon exists
            if (!string.IsNullOrEmpty(appIconBase64))
            {
                await SetNotificationIcon(builder, appIconBase64, "appIcon.png");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set app notification icon");
        }
    }

    private async Task SetNotificationIcon(AppNotificationBuilder builder, string iconBase64, string fileName)
    {
        try
        {
            var fileUri = await ImageUtils.SaveBase64ToFileAsync(iconBase64, fileName);
            builder.SetAppLogoOverride(fileUri, AppNotificationImageCrop.Circle);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set notification icon");
        }
    }
}
