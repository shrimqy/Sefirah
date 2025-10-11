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

            if (!string.IsNullOrEmpty(message.LargeIcon))
            {
                var fileUri = await IconUtils.SaveBase64ToFileAsync(message.LargeIcon, "largeIcon.png");
                builder.SetAppLogoOverride(fileUri, AppNotificationImageCrop.Circle);
            }
            else if (!string.IsNullOrEmpty(message.AppPackage))
            {
                var iconUri = await IconUtils.GetAppIconUriAsync(message.AppPackage);
                if (iconUri is not null)
                {
                    builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);
                }
            }

            // Handle actions
            foreach (var action in message.Actions)
            {
                if (action is null) continue;

                if (action.IsReplyAction)
                {
                    builder
                        .AddTextBox("textBox", "ReplyPlaceholder".GetLocalizedResource(), "")
                        .AddButton(new AppNotificationButton("SendButton".GetLocalizedResource())
                            .AddArgument("notificationType", ToastNotificationType.RemoteNotification)
                            .AddArgument("tag", message.NotificationKey)
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
                        .AddArgument("tag", message.NotificationKey)
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

    public async void ShowFileTransferNotification(string subtitle, string fileName, string transferId, uint notificationSequence, double? progress = null)
    {
        try
        {
            // if transfer is in progress, update existing notification
            if (progress.HasValue && progress > 0 && progress < 100)
            {
                var progressData = new AppNotificationProgressData(notificationSequence)
                {
                    Title = fileName,
                    Value = progress.Value / 100,
                    ValueStringOverride = $"{progress.Value:F0}%",
                    Status = subtitle 
                };
                await AppNotificationManager.Default.UpdateAsync(progressData, transferId, Constants.Notification.FileTransferGroup);
            }
            else
            {
                var builder = new AppNotificationBuilder()
                    .AddText("FileTransferNotification.Title".GetLocalizedResource())
                    .SetTag(transferId)
                    .SetGroup(Constants.Notification.FileTransferGroup)
                    .MuteAudio()
                    .AddButton(new AppNotificationButton("FileTransferNotificationAction.Cancel".GetLocalizedResource())
                        .AddArgument("notificationType", ToastNotificationType.FileTransfer)
                        .AddArgument("action", "cancel"))
                    .AddProgressBar(new AppNotificationProgressBar()
                        .BindTitle()
                        .BindValue()
                        .BindValueStringOverride()
                        .BindStatus());

                var notification = builder.BuildNotification();
                notification.ExpiresOnReboot = true;

                // Set initial progress data
                notification.Progress = new AppNotificationProgressData(notificationSequence)
                {
                    Title = fileName,
                    Value = 0,
                    ValueStringOverride = "0%",
                    Status = subtitle
                };

                AppNotificationManager.Default.Show(notification);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Notification failed, Progress: {progress}, Sequence: {notificationSequence}", ex);
        }
    }


    /// <inheritdoc />
    public async void ShowCompletedFileTransferNotification(string subtitle, string transferId, string? filePath = null, string? folderPath = null)
    {
        // TODO: show hero image if available   
        try
        {
            await Task.Delay(500);
            var builder = new AppNotificationBuilder()
                .AddText("FileTransferNotification.Completed".GetLocalizedResource())
                .AddText(subtitle)
                .SetTag(transferId)
                .SetGroup(Constants.Notification.FileTransferGroup);

            if (!string.IsNullOrEmpty(filePath))
            {
                builder.AddButton(new AppNotificationButton("FileTransferNotificationAction.OpenFile".GetLocalizedResource())
                    .AddArgument("notificationType", ToastNotificationType.FileTransfer)
                    .AddArgument("action", "openFile")
                    .AddArgument("filePath", filePath));
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                builder.AddButton(new AppNotificationButton("FileTransferNotificationAction.OpenFolder".GetLocalizedResource())
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
    public void ShowClipboardNotification(string title, string text, string? iconPath = null)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(text)
                .SetTag($"clipboard_{DateTime.Now.Ticks}")
                .SetGroup("clipboard");

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
    public void ShowClipboardNotificationWithActions(string title, string text, string? actionLabel = null, string? actionData = null)
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
            logger.LogInformation("Notification invoked - Arguments: {Arguments}", string.Join(", ", args.Arguments.Select(x => $"{x.Key}={x.Value}")));

            if (!args.Arguments.TryGetValue("notificationType", out var notificationType)) return;
            
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

    private static async void HandleClipboardNotification(AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments.TryGetValue("uri", out var uriString) && Uri.TryCreate(uriString, UriKind.Absolute, out Uri? uri) && ClipboardService.IsValidWebUrl(uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private static void HandleFileTransferNotification(AppNotificationActivatedEventArgs args)
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
                case "cancel":
                    var fileTransferService = Ioc.Default.GetRequiredService<IFileTransferService>();
                    fileTransferService.CancelTransfer();
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
        if (device is null) return;

        var notificationKey = args.Arguments["tag"];
        switch (actionType)
        {
            case "Reply" when args.UserInput.TryGetValue("textBox", out var replyText):
                if (args.Arguments.TryGetValue("replyResultKey", out var replyResultKey))
                {
                    NotificationActionUtils.ProcessReplyAction(sessionManager, logger, device, notificationKey, replyResultKey, replyText);
                }
                break;
            case "Click":
                if (args.Arguments.TryGetValue("actionIndex", out var actionIndexStr))
                {
                    NotificationActionUtils.ProcessClickAction(sessionManager, logger, device, notificationKey, int.Parse(actionIndexStr));
                }
                break;
        }
    }

    /// <inheritdoc />
    public async Task RemoveNotificationByTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        await AppNotificationManager.Default.RemoveByTagAsync(tag);
    }

    /// <inheritdoc />
    public async Task RemoveNotificationsByGroup(string? groupKey)
    {
        if (string.IsNullOrEmpty(groupKey)) return;
        await AppNotificationManager.Default.RemoveByGroupAsync(groupKey);
    }

    public async Task RemoveNotificationsByTagAndGroup(string? tag, string? groupKey)
    {
        if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(groupKey)) return;
        await AppNotificationManager.Default.RemoveByTagAndGroupAsync(tag, groupKey);
    }

    /// <inheritdoc />
    public async Task ClearAllNotifications()
    {
        await AppNotificationManager.Default.RemoveAllAsync();
    }
}
