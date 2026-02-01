using System.Collections.Concurrent;
using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Windows.Data.Xml.Dom;
using Windows.System;
using Windows.UI.Notifications;
using Notification = Sefirah.Data.Models.Notification;

namespace Sefirah.Services;

public class NotificationService(
    ILogger logger,
    ISessionManager sessionManager,
    IDeviceManager deviceManager,
    IPlatformNotificationHandler platformNotificationHandler,
    RemoteAppRepository remoteAppsRepository) : INotificationService
{
    private readonly ConcurrentDictionary<string, List<Notification>> deviceNotifications = [];
    private readonly ConcurrentDictionary<string, SemaphoreSlim> notificationLocks = [];

    private readonly ObservableCollection<Notification> activeNotifications = [];
    public ReadOnlyObservableCollection<Notification> NotificationHistory => new(activeNotifications);

    private SemaphoreSlim GetNotificationLock(string deviceId) =>
        notificationLocks.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));

    public async Task Initialize()
    {
        ClearBadge();
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        // deviceManager.ActiveDeviceChanged += OnActiveDeviceChanged;
    }

    private async void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (device.IsConnected)
        {
            await ClearHistoryAsync(device);
        }
    }

    public async Task HandleNotificationMessage(PairedDevice device, NotificationMessage message)
    {
        if (!device.DeviceSettings.NotificationSync) return;

        var notificationLock = GetNotificationLock(device.Id);
        await notificationLock.WaitAsync();
        try
        {
            if (message.NotificationType is NotificationType.Removed)
            {
                await HandleRemovedNotificationAsync(device, message);
                return;
            }

            if (message.Title is null || message.AppPackage is null) return;

            var filter = await remoteAppsRepository.GetOrCreateAppNotificationFilter(
                device.Id, message.AppPackage, message.AppName!, message.AppIcon);

            if (filter is NotificationFilter.Disabled) return;

            await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
            {
                var notifications = deviceNotifications.GetOrAdd(device.Id, _ => []);
                var notification = await Notification.FromMessage(message);

                if (message.NotificationType is NotificationType.New && filter is NotificationFilter.ToastFeed)
                {
                    await HandleNewNotificationAsync(device, message, notifications, notification);
                }
                else if (filter is NotificationFilter.Feed or NotificationFilter.ToastFeed)
                {
                    notifications.Add(notification);
                }
                else
                {
                    return;
                }

                SortNotifications(notifications);
                SyncActiveNotifications(device);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling notification message");
        }
        finally
        {
            notificationLock.Release();
        }
    }

    private async Task HandleNewNotificationAsync(
        PairedDevice device,
        NotificationMessage message,
        List<Notification> notifications,
        Notification notification)
    {
        var existing = notifications.FirstOrDefault(n => n.Key == notification.Key);
        if (existing is not null)
        {
            var index = notifications.IndexOf(existing);
            notification.Pinned = existing.Pinned;
            notifications[index] = notification;
        }
        else
        {
            notifications.Insert(0, notification);
        }
        if (device.DeviceSettings.IgnoreWindowsApps && await IsAppActiveAsync(message.AppName!)) return;

        await platformNotificationHandler.ShowRemoteNotification(message, device.Id);
    }

    private async Task HandleRemovedNotificationAsync(PairedDevice device, NotificationMessage message)
    {
        if (!deviceNotifications.TryGetValue(device.Id, out var notifications)) return;

        var notification = notifications.FirstOrDefault(n => n.Key == message.NotificationKey);
        if (notification is null || notification.Pinned) return;

        await App.MainWindow.DispatcherQueue.EnqueueAsync(() => notifications.Remove(notification));
        SyncActiveNotifications(device);
    }


    public void TogglePinNotification(Notification notification)
    {
        var activeDevice = deviceManager.ActiveDevice!;

        App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            if (!deviceNotifications.TryGetValue(activeDevice.Id, out var notifications)) return;

            var index = notifications.IndexOf(notification);
            if (index < 0) return;

            notification.Pinned = !notification.Pinned;
            notifications[index] = notification;

            SortNotifications(notifications);
            SyncActiveNotifications(activeDevice);
        });
    }

    public void RemoveNotification(PairedDevice device, Notification notification)
    {
        if (notification.Pinned) return;
        if (!deviceNotifications.TryGetValue(device.Id, out var notifications)) return;

        try
        {
            App.MainWindow.DispatcherQueue.EnqueueAsync(() => notifications.Remove(notification));
            platformNotificationHandler.RemoveNotificationsByTagAndGroup(notification.Tag, notification.GroupKey);
            SyncActiveNotifications(device);

            if (device.IsConnected)
            {
                device.SendMessage(new NotificationMessage
                {
                    NotificationKey = notification.Key,
                    NotificationType = NotificationType.Removed
                });
            }

            logger.LogDebug("Removed notification with key: {NotificationKey} from device {DeviceId}",
                notification.Key, device.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing notification");
        }
    }

    public void ProcessReplyAction(PairedDevice device, string notificationKey, string replyResultKey, string replyText)
    {
        if (!device.IsConnected) return;

        device.SendMessage(new ReplyAction
        {
            NotificationKey = notificationKey,
            ReplyResultKey = replyResultKey,
            ReplyText = replyText
        });

        logger.LogDebug("Sent reply action for notification {NotificationKey} to device {DeviceId}",
            notificationKey, device.Id);
    }

    public void ProcessClickAction(PairedDevice device, string notificationKey, int actionIndex)
    {
        if (!device.IsConnected) return;

        device.SendMessage(new NotificationAction
        {
            NotificationKey = notificationKey,
            ActionIndex = actionIndex,
            IsReplyAction = false
        });

        logger.LogDebug("Sent click action for notification {NotificationKey} to device {DeviceId}",
            notificationKey, device.Id);
    }

    public async void ClearAllNotification()
    {
        var activeDevice = deviceManager.ActiveDevice!;
        try
        {
            await ClearHistoryAsync(activeDevice);
            App.MainWindow.DispatcherQueue.TryEnqueue(() => activeNotifications.Clear());

            if (!activeDevice.IsConnected) return;

            activeDevice.SendMessage(new CommandMessage { CommandType = CommandType.ClearNotifications });
            logger.LogInformation("Cleared all notifications for device {DeviceId}", activeDevice.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing all notifications");
        }
    }

    public async Task ClearHistoryAsync(PairedDevice device)
    {
        var notificationLock = GetNotificationLock(device.Id);
        await notificationLock.WaitAsync();
        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                if (!deviceNotifications.TryGetValue(device.Id, out var notifications)) return;

                foreach (var notification in notifications)
                {
                    platformNotificationHandler.RemoveNotificationsByTagAndGroup(notification.Tag, notification.GroupKey);
                }

                notifications.Clear();
                ClearBadge();
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing history");
        }
        finally
        {
            notificationLock.Release();
        }
    }

    private void SyncActiveNotifications(PairedDevice device)
    {
        var activeDevice = deviceManager.ActiveDevice;
        if (activeDevice is null || activeDevice.Id != device.Id) return;

        App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            activeNotifications.Clear();

            if (deviceNotifications.TryGetValue(activeDevice.Id, out var notifications))
            {
                activeNotifications.AddRange(notifications);
                UpdateBadge(notifications.Count, activeDevice.DeviceSettings.ShowBadge);
            }
        });
    }

    private static void SortNotifications(List<Notification> notifications)
    {
        notifications.Sort((a, b) =>
        {
            var pinnedCompare = b.Pinned.CompareTo(a.Pinned);
            return pinnedCompare != 0 ? pinnedCompare : b.TimeStamp.CompareTo(a.TimeStamp);
        });
    }

    private static void UpdateBadge(int count, bool showBadge)
    {
        if (!showBadge) return;

        var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
        var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge")!;
        badgeElement.SetAttribute("value", count.ToString());

        var badgeUpdater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
        badgeUpdater.Update(new BadgeNotification(badgeXml));
    }

    private static void ClearBadge()
    {
        BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
    }

    private readonly List<string> IgnoreWindowsApps =
    [
        "Instagram"
    ];

    private async Task<bool> IsAppActiveAsync(string appName)
    {
        if (IgnoreWindowsApps.Contains(appName)) return false;

        try
        {
#if WINDOWS
            var diagnosticInfo = await AppDiagnosticInfo.RequestInfoAsync();
            return diagnosticInfo.Any(info =>
                info.AppInfo.DisplayInfo.DisplayName.Contains(appName, StringComparison.OrdinalIgnoreCase));
#else
            return false;
#endif
        }
        catch (Exception)
        {
            return false;
        }
    }
}

