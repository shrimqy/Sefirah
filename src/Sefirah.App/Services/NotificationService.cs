using CommunityToolkit.WinUI;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using Sefirah.App.Extensions;
using Sefirah.App.Utils.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using static Sefirah.App.Services.ToastNotificationService;

namespace Sefirah.App.Services;

public class NotificationService(
    ILogger logger,
    ISessionManager sessionManager,
    IUserSettingsService userSettingsService,
    IRemoteAppsRepository remoteAppsRepository) : INotificationService
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly ObservableCollection<NotificationMessage> notifications = [];
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    public ReadOnlyObservableCollection<NotificationMessage> NotificationHistory => new(notifications);
    public event EventHandler<NotificationMessage>? NotificationReceived;

    public async Task HandleNotificationMessage(NotificationMessage message)
    {
        if (!userSettingsService.FeatureSettingsService.NotificationSyncEnabled) return;

        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            logger.Debug("Processing notification message from {0}", message.AppName);
            
            var filter = await remoteAppsRepository.GetNotificationFilterAsync(message.AppPackage)
                ?? await remoteAppsRepository.AddNewAppNotificationFilter(message.AppPackage, message.AppName!, !string.IsNullOrEmpty(message.AppIcon) ? Convert.FromBase64String(message.AppIcon) : null!);
            
            if (filter == NotificationFilter.Disabled) return;

            if (message.Title != null)
            {
                if (message.NotificationType == nameof(NotificationType.New) && filter == NotificationFilter.ToastFeed)
                {
                    await dispatcher.EnqueueAsync(async () =>
                    {
                        // Check for existing notification
                        var existingNotification = notifications.FirstOrDefault(n => 
                            n.NotificationKey == message.NotificationKey);

                        if (existingNotification != null)
                        {
                            // Update existing notification
                            var index = notifications.IndexOf(existingNotification);
                            notifications[index] = message;
                        }
                        else
                        {
                            // Add new notification
                            notifications.Insert(0, message);
                        }

                        if (userSettingsService.FeatureSettingsService.IgnoreWindowsApps && await IsAppActiveAsync(message.AppName!) || !userSettingsService.FeatureSettingsService.ShowNotificationToast ||
                        userSettingsService.FeatureSettingsService.IgnoreNotificationDuringDnd && deviceManager.CurrentDeviceStatus?.IsDndEnabled == true) return;
                        await ShowWindowsNotification(message);
                    });
                }
                else if ((message.NotificationType == nameof(NotificationType.Active) || message.NotificationType == nameof(NotificationType.New)) 
                    && filter == NotificationFilter.Feed || filter == NotificationFilter.ToastFeed)
                {
                    await dispatcher.EnqueueAsync(() =>
                    {
                        notifications.Add(message);
                    });
                }
                else
                {
                    logger.Warn("Notification from {0} does not meet criteria for Windows feed display", message.AppName);
                }
            }
            else if (message.NotificationType == nameof(NotificationType.Removed))
            {
                await RemoveNotification(message.NotificationKey, false);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error handling notification message", ex);
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<bool> IsAppActiveAsync(string appName)
    {
        try
        {
            // Get all running apps
            var diagnosticInfo = await AppDiagnosticInfo.RequestInfoAsync();
            var isAppActive = diagnosticInfo.Any(info => 
                info.AppInfo.DisplayInfo.DisplayName.Equals(appName, StringComparison.OrdinalIgnoreCase));
            return isAppActive;
        }
        catch (Exception ex)
        {
            logger.Error($"Error checking if app '{appName}' is active", ex);
            return false;
        }
    }

    private async Task ShowWindowsNotification(NotificationMessage message)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(message.AppName, new AppNotificationTextProperties().SetMaxLines(1))
                .AddText(message.Title)
                .AddText(message.Text)
                .SetTag(message.Tag ?? string.Empty)
                .SetGroup(message.GroupKey ?? string.Empty);


            // Handle icons
            if (!string.IsNullOrEmpty(message.LargeIcon))
            {
                await SetNotificationIcon(builder, message.LargeIcon, "largeIcon.png");
            }
            else if (!string.IsNullOrEmpty(message.AppIcon))
            {
                await SetNotificationIcon(builder, message.AppIcon, "appIcon.png");
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
                                .SetInputId("textBox"));
                }
                else
                {
                    builder.AddButton(new AppNotificationButton(action.Label)
                        .AddArgument("notificationType", ToastNotificationType.RemoteNotification)
                        .AddArgument("action", "Click")
                        .AddArgument("actionIndex", action.ActionIndex.ToString())
                        .AddArgument("notificationKey", message.NotificationKey));
                }
            }

            var notification = builder.BuildNotification();
            notification.ExpiresOnReboot = true;
            AppNotificationManager.Default.Show(notification);
            logger.Debug("Windows notification shown for {0}", message.AppName);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to show Windows notification for {0}", message.AppName, ex);
            throw;
        }
    }

    private async Task SetNotificationIcon(AppNotificationBuilder builder, string iconBase64, string fileName)
    {
        try
        {
            // Save file and get URI in one operation
            var fileUri = await SaveBase64ToFileAsync(iconBase64, fileName);
            builder.SetAppLogoOverride(fileUri, AppNotificationImageCrop.Circle);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to set notification icon", ex);
        }
    }

    private static async Task<Uri> SaveBase64ToFileAsync(string base64, string fileName)
    {
        var bytes = Convert.FromBase64String(base64);
        var localFolder = ApplicationData.Current.LocalFolder;
        var file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            using var dataWriter = new DataWriter(stream);
            dataWriter.WriteBytes(bytes);
            await dataWriter.StoreAsync();
        }

        return new Uri($"ms-appdata:///local/{fileName}");
    }

    public async Task  RemoveNotification(string notificationKey, bool isRemote)
    {
        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                var notificationToRemove = notifications.FirstOrDefault(n =>
                    n.NotificationKey == notificationKey);

                if (notificationToRemove != null)
                {
                    notifications.Remove(notificationToRemove);
                    logger.Debug("Removed notification with key: {0}", notificationKey);

                    if (!string.IsNullOrEmpty(notificationToRemove.Tag))
                    {
                        await AppNotificationManager.Default.RemoveByTagAsync(notificationToRemove.Tag);
                        logger.Debug("Removed Windows notification by tag: {0}", notificationToRemove.Tag);
                    }
                    else if (!string.IsNullOrEmpty(notificationToRemove.GroupKey))
                    {
                        await AppNotificationManager.Default.RemoveByGroupAsync(notificationToRemove.GroupKey);
                        logger.Debug("Removed Windows notification by group: {0}", notificationToRemove.GroupKey);
                    }

                    if (!isRemote)
                    {
                        notificationToRemove.NotificationType = nameof(NotificationType.Removed);
                        string jsonMessage = SocketMessageSerializer.Serialize(notificationToRemove);
                        sessionManager.SendMessage(jsonMessage);
                        logger.Debug("Sent notification removal message to remote device");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error removing notification", ex);
                throw;
            }
        });
    }

    public async Task ClearAllNotification()
    {
        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                notifications.Clear();
                var command = new Misc { MiscType = nameof(MiscType.ClearNotifications) };
                string jsonMessage = SocketMessageSerializer.Serialize(command);
                sessionManager.SendMessage(jsonMessage);
                logger.Info("Cleared all notifications");
            }
            catch (Exception ex)
            {
                logger.Error("Error clearing all notifications", ex);
                throw;
            }
        });
    }

    public async Task ClearHistory()
    {
        await dispatcher.EnqueueAsync(() =>
        {
            try
            {
                notifications.Clear();
            }
            catch (Exception ex)
            {
                logger.Error("Error clearing history", ex);
                throw;
            }
        });
    }


    public void RegisterNotifications()
    {
        // Register the notification manager
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        AppNotificationManager.Default.Register();
    }

    private async void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            if (!args.Arguments.TryGetValue("action", out var actionType))
                return;

            var notificationKey = args.Arguments["notificationKey"];
            var actionIndex = int.Parse(args.Arguments["actionIndex"]);

            switch (actionType)
            {
                case "Reply" when args.UserInput.TryGetValue("textBox", out var replyText):
                    ProcessReplyAction(notificationKey, args.Arguments["replyResultKey"], replyText);
                    break;
                
                case "click":
                    ProcessClickAction(notificationKey, actionIndex);
                    break;
                
                default:
                    logger.Warn($"Unhandled action type: {actionType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error("Failed to handle notification action", ex);
        }
    }

    public void ProcessReplyAction(string notificationKey, string ReplyResultKey, string replyText)
    {
        var replyAction = new ReplyAction
        {
            NotificationKey = notificationKey,
            ReplyResultKey = ReplyResultKey,
            ReplyText = replyText,
        };

        sessionManager.SendMessage(SocketMessageSerializer.Serialize(replyAction));
    }

    public void ProcessClickAction(string notificationKey, int actionIndex)
    {
        var notificationAction = new NotificationAction
        {
            NotificationKey = notificationKey,
            ActionIndex = actionIndex,
            IsReplyAction = false
        };

        sessionManager.SendMessage(SocketMessageSerializer.Serialize(notificationAction));
    }
}
