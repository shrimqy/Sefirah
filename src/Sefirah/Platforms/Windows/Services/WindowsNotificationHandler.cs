using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Sefirah.Platforms.Windows.Calling;
using Sefirah.Services;
using Sefirah.Utils;
using Windows.System;
using static Sefirah.Constants;

namespace Sefirah.Platforms.Windows.Services;

/// <summary>
/// Windows implementation of the platform notification handler
/// </summary>
public class WindowsNotificationHandler(
    ILogger logger,
    IDeviceManager deviceManager) : IPlatformNotificationHandler
{
    /// <inheritdoc />
    public async Task ShowRemoteNotification(Data.Models.NotificationInfo message, string deviceId)
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
            
            // add textbox if a reply action exists
            if (!string.IsNullOrEmpty(message.ReplyResultKey))
            {
                builder
                    .AddTextBox("textBox", "ReplyPlaceholder".GetLocalizedResource(), "")
                    .AddButton(new AppNotificationButton("SendButton".GetLocalizedResource())
                        .AddArgument("notificationType", ToastNotificationType.RemoteNotification)
                        .AddArgument("tag", message.NotificationKey)
                        .AddArgument("replyResultKey", message.ReplyResultKey)
                        .AddArgument("action", "Reply")
                        .AddArgument("deviceId", deviceId).SetInputId("textBox"));
            }

            // Handle actions
            foreach (var action in message.Actions)
            {
                builder.AddButton(new AppNotificationButton(action.Label)
                    .AddArgument("notificationType", ToastNotificationType.RemoteNotification)
                    .AddArgument("action", "Click")
                    .AddArgument("actionIndex", action.ActionIndex.ToString())
                    .AddArgument("tag", message.NotificationKey)
                    .AddArgument("deviceId", deviceId));
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

    public async void ShowFileTransferNotification(
        string notificationTitle,
        string progressTitle,
        string status,
        string transferId,
        uint notificationSequence,
        double progress)
    {
        try
        {
            // if transfer is in progress, update existing notification
            if (progress > 0 && progress < 100)
            {
                var progressData = new AppNotificationProgressData(notificationSequence)
                {
                    Title = progressTitle,
                    Value = progress / 100,
                    ValueStringOverride = $"{progress:F0}%",
                    Status = status
                };
                await AppNotificationManager.Default.UpdateAsync(progressData, transferId, Constants.Notification.FileTransferGroup);
            }
            else
            {
                var builder = new AppNotificationBuilder()
                    .AddText(notificationTitle)
                    .SetTag(transferId)
                    .SetGroup(Constants.Notification.FileTransferGroup)
                    .MuteAudio()
                    .AddButton(new AppNotificationButton("FileTransferNotificationAction.Cancel".GetLocalizedResource())
                        .AddArgument("notificationType", ToastNotificationType.FileTransfer)
                        .AddArgument("action", "cancel")
                        .AddArgument("tag", transferId))
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
                    Title = progressTitle,
                    Value = 0,
                    ValueStringOverride = $"{progress:F0}%",
                    Status = status
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
    public Task ShowCallNotification(string title, string text, string tag, CallState callState, Uri? icon = null)
    {
        try
        {
            var soundEvent = callState is CallState.MissedCall
                ? AppNotificationSoundEvent.Default
                : AppNotificationSoundEvent.Call;

            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(text)
                .SetTag(tag)
                .SetAudioEvent(soundEvent);

            if (icon is not null)
            {
                builder.SetAppLogoOverride(icon, AppNotificationImageCrop.Circle);
            }

            var notification = builder.BuildNotification();
            notification.ExpiresOnReboot = true;
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show call notification");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowCallNotification(string callId, string title, string subtitle, Uri? icon = null)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title, new AppNotificationTextProperties().SetMaxLines(1))
                .AddText(subtitle)
                .SetTag(callId)
                .SetGroup(Notification.IncomingPhoneCallGroup)
                .SetAudioEvent(AppNotificationSoundEvent.Call)
                .AddButton(new AppNotificationButton("ConnectionRequestAcceptButton".GetLocalizedResource())
                    .SetButtonStyle(AppNotificationButtonStyle.Success)
                    .AddArgument("notificationType", ToastNotificationType.IncomingPhoneCall)
                    .AddArgument("action", "accept")
                    .AddArgument("callId", callId))
                .AddButton(new AppNotificationButton("ConnectionRequestRejectButton".GetLocalizedResource())
                    .SetButtonStyle(AppNotificationButtonStyle.Critical)
                    .AddArgument("notificationType", ToastNotificationType.IncomingPhoneCall)
                    .AddArgument("action", "decline")
                    .AddArgument("callId", callId));

            if (icon is not null)
            {
                builder.SetAppLogoOverride(icon, AppNotificationImageCrop.Circle);
            }

            var notification = builder.BuildNotification();
            notification.ExpiresOnReboot = true;
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show incoming phone call notification for {CallId}", callId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void ShowClipboardNotification(string title, string text, string? actionLabel = null, string? actionData = null)
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
            logger.LogInformation("Notification invoked - ArgumentCount: {ArgumentCount}, Keys: {Keys}",
                args.Arguments.Count,
                string.Join(", ", args.Arguments.Keys));

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

                case ToastNotificationType.Update:
                    HandleUpdateNotification(args);
                    break;

                case ToastNotificationType.IncomingPhoneCall:
                    await HandleIncomingPhoneCallNotificationAsync(args);
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

    private static async void HandleUpdateNotification(AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments.TryGetValue("action", out var action) && action == "download")
        {
            var updateService = Ioc.Default.GetRequiredService<IUpdateService>();
            await updateService.DownloadUpdatesAsync();
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
                    // Get transferId from notification tag
                    if (args.Arguments.TryGetValue("tag", out string? transferIdStr) && Guid.TryParse(transferIdStr, out Guid transferId))
                    {
                        fileTransferService.CancelTransfer(transferId);
                    }
                    break;
            }
        }
    }

    private async Task HandleIncomingPhoneCallNotificationAsync(AppNotificationActivatedEventArgs args)
    {
        if (!args.Arguments.TryGetValue("action", out var action) ||
            !args.Arguments.TryGetValue("callId", out var callId) ||
            string.IsNullOrWhiteSpace(callId))
        {
            return;
        }

        await AppNotificationManager.Default.RemoveByTagAsync(callId).AsTask();

        var call = PhoneCall.FromCallId(callId);
        if (call is null)
        {
            logger.LogInformation("Incoming call notification action {Action}: no session for {CallId}", action, callId);
            return;
        }

        if (action == "decline")
        {
            try
            {
                await call.RejectIncomingAsync();
            }
            finally
            {
                call.Dispose();
            }

            return;
        }

        if (action != "accept")
        {
            call.Dispose();
            return;
        }

        try
        {
            await call.AcceptIncomingAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AcceptIncomingAsync failed for {CallId}", callId);
            call.Dispose();
            return;
        }
        call.Dispose();
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
                    NotificationActionUtils.ProcessReplyAction(logger, device, notificationKey, replyResultKey, replyText);
                }
                break;
            case "Click":
                if (args.Arguments.TryGetValue("actionIndex", out var actionIndexStr))
                {
                    NotificationActionUtils.ProcessClickAction(logger, device, notificationKey, int.Parse(actionIndexStr));
                }
                break;
        }
    }

    /// <inheritdoc />
    public async Task RemoveNotificationById(uint notificationId)
    {
        await AppNotificationManager.Default.RemoveByIdAsync(notificationId);
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
