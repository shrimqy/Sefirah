using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Extensions;
using Sefirah.Utils;
using Uno.Logging;

namespace Sefirah.Platforms.Windows;

/// <summary>
/// Windows implementation of the platform notification handler
/// </summary>
public class WindowsNotificationHandler(ILogger logger) : IPlatformNotificationHandler
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

    public async Task ShowTransferNotification(string title, string message, string fileName, uint notificationSequence, double? progress = null, bool isReceiving = true, bool silent = false)
    {
        string tag = isReceiving ? "file-receive" : "file-send";
        try
        {
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
            logger.Error($"Notification failed - Tag: {tag}, Progress: {progress}, Sequence: {notificationSequence}", ex);
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
        // Registration is handled by ToastNotificationService
        await Task.CompletedTask;
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
