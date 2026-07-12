using System.Collections.Concurrent;
using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Windows.Data.Xml.Dom;
using Windows.System;
using Windows.UI.Notifications;
using Notification = Sefirah.Data.Models.Notification;

namespace Sefirah.Features;

public class NotificationFeature(
    ILogger logger,
    ISessionManager sessionManager,
    IDeviceManager deviceManager,
    IPlatformNotificationHandler platformNotificationHandler,
    RemoteAppRepository remoteAppsRepository,
    NotificationRepository notificationRepository) : INotificationFeature
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> notificationLocks = [];

    public ObservableCollection<Notification> Notifications { get; } = [];

    private SemaphoreSlim GetNotificationLock(string deviceId) => notificationLocks.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));

    public async Task InitializeAsync()
    {
        ClearBadge();
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        deviceManager.ActiveDeviceChanged += OnActiveDeviceChanged;

        await LoadNotificationsFromDatabase(deviceManager.ActiveDevice);
    }

    private async void OnActiveDeviceChanged(object? sender, PairedDevice? device)
    {
        await LoadNotificationsFromDatabase(device);
    }

    public async Task LoadNotificationsFromDatabase(PairedDevice? device)
    {
        if (device is null)
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => Notifications.Clear());
            ClearBadge();
            return;
        }

        var notificationLock = GetNotificationLock(device.Id);
        await notificationLock.WaitAsync();
        try
        {
            var notifications = await notificationRepository.GetNotificationsAsync(device.Id);
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                Notifications.Clear();
                Notifications.AddRange(notifications);
                UpdateBadge();
            });
        }
        catch (Exception ex)
        {
            logger.Error($"Error loading notifications from database for device {device.Id}", ex);
        }
        finally
        {
            notificationLock.Release();
        }
    }

    private async void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (device.IsConnected)
        {
            await ClearHistoryAsync(device);
        }
    }

    public async Task HandleNotificationMessage(PairedDevice device, Data.Models.NotificationInfo message)
    {
        if (!device.DeviceSettings.NotificationSync) return;

        var notificationLock = GetNotificationLock(device.Id);
        await notificationLock.WaitAsync();
        try
        {
            if (message.InfoType is NotificationInfoType.Removed)
            {
                await HandleRemovedNotification(device, message);
                return;
            }

            var filter = await remoteAppsRepository.GetOrCreateAppNotificationFilter(device.Id, message.AppPackage!, message.AppName!, message.AppIcon);

            if (filter is NotificationFilter.Disabled) return;

            await notificationRepository.SaveNotificationAsync(message, device.Id);

            var notification = notificationRepository.GetNotification(device.Id, message.NotificationKey)?.ToNotification();
            if (notification is null) return;

            await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
            {
                if (device == deviceManager.ActiveDevice)
                    HandleNotificationForActiveDevice(notification);

                if (message.InfoType is NotificationInfoType.New && filter is NotificationFilter.ToastFeed)
                {
                    if (device.DeviceSettings.IgnoreWindowsApps && await IsAppActiveAsync(message.AppName!)) return;

                    await platformNotificationHandler.ShowRemoteNotification(message, device.Id);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error("Error handling notification message", ex);
        }
        finally
        {
            notificationLock.Release();
        }
    }

    private void HandleNotificationForActiveDevice(Notification notification)
    {
        var existing = Notifications.FirstOrDefault(n => n.Key == notification.Key);
        if (existing is not null)
        {
            var index = Notifications.IndexOf(existing);
            Notifications[index] = notification;
        }
        else
        {
            Notifications.Insert(0, notification);
        }

        SortNotifications();
        UpdateBadge();
    }

    private async Task HandleRemovedNotification(PairedDevice device, Data.Models.NotificationInfo message)
    {
        var deleted = await notificationRepository.DeleteNotificationAsync(device.Id, message.NotificationKey);
        if (!deleted) return;

        if (device == deviceManager.ActiveDevice)
        {
            var notification = Notifications.FirstOrDefault(n => n.Key == message.NotificationKey);
            if (notification is not null)
            {
                await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                {
                    Notifications.Remove(notification);
                    UpdateBadge();
                });
            }
        }
    }


    public async void TogglePinNotification(Notification notification)
    {
        var activeDevice = deviceManager.ActiveDevice!;

        await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            var index = Notifications.IndexOf(notification);
            notification.Pinned = !notification.Pinned;
            Notifications[index] = notification;
            SortNotifications();
        });

        UpdateBadge();
        await notificationRepository.UpdateNotificationPinAsync(notification, activeDevice.Id);
    }

    public async void RemoveNotification(PairedDevice device, Notification notification)
    {
        try
        {
            var result = await notificationRepository.DeleteNotificationAsync(device.Id, notification.Key);
            if (!result) return;

            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => Notifications.Remove(notification));
            UpdateBadge();

            await platformNotificationHandler.RemoveNotificationsByTagAndGroup(notification.Tag, notification.GroupKey);

            if (device.IsConnected)
            {
                device.SendMessage(new Data.Models.NotificationInfo
                {
                    NotificationKey = notification.Key,
                    InfoType = NotificationInfoType.Removed
                });
            }

            logger.Debug($"Removed notification with key: {notification.Key} from device {device.Id}");
        }
        catch (Exception ex)
        {
            logger.Error("Error removing notification", ex);
        }
    }

    public void ProcessReplyAction(PairedDevice device, string notificationKey, string replyResultKey, string replyText)
    {
        if (!device.IsConnected) return;

        device.SendMessage(new NotificationReply
        {
            NotificationKey = notificationKey,
            ReplyResultKey = replyResultKey,
            ReplyText = replyText
        });

        logger.Debug($"Sent reply action for notification {notificationKey} to device {device.Id}");
    }

    public void ProcessClickAction(PairedDevice device, string notificationKey, int actionIndex)
    {
        if (!device.IsConnected) return;

        device.SendMessage(new NotificationAction
        {
            NotificationKey = notificationKey,
            ActionIndex = actionIndex,
        });

        logger.Debug($"Sent click action for notification {notificationKey} to device {device.Id}");
    }

    public async void ClearAllNotification()
    {
        var activeDevice = deviceManager.ActiveDevice!;
        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
            {
                foreach (var notification in Notifications.Where(n => !n.Pinned).ToList())
                {
                    await platformNotificationHandler.RemoveNotificationsByTagAndGroup(notification.Tag, notification.GroupKey);
                }
                await ClearHistoryAsync(activeDevice);
            });

            if (!activeDevice.IsConnected) return;

            activeDevice.SendMessage(new ClearNotifications());
            logger.Info($"Cleared all notifications for device {activeDevice.Id}");
        }
        catch (Exception ex)
        {
            logger.Error("Error clearing all notifications", ex);
        }
    }

    private async Task ClearHistoryAsync(PairedDevice device)
    {
        var notificationLock = GetNotificationLock(device.Id);
        await notificationLock.WaitAsync();
        try
        {
            await notificationRepository.ClearHistoryForDeviceAsync(device.Id);

            if (device == deviceManager.ActiveDevice)
            {
                await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                {
                    foreach (var n in Notifications.Where(x => !x.Pinned).ToList())
                    {
                        Notifications.Remove(n);
                    }
                });
                UpdateBadge();
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error clearing history", ex);
        }
        finally
        {
            notificationLock.Release();
        }
    }

    private void SortNotifications()
    {
        var sortedNotifications = Notifications
            .OrderByDescending(n => n.Pinned)
            .ThenByDescending(n => n.TimestampMillis)
            .ToList();

        for (var targetIndex = 0; targetIndex < sortedNotifications.Count; targetIndex++)
        {
            var targetNotification = sortedNotifications[targetIndex];
            var currentIndex = Notifications.IndexOf(targetNotification);
            if (currentIndex != targetIndex)
                Notifications.Move(currentIndex, targetIndex);
        }
    }

    private void UpdateBadge()
    {
        if (deviceManager.ActiveDevice?.DeviceSettings.ShowBadge is true)
        {
            var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge")!;
            badgeElement.SetAttribute("value", Notifications.Count.ToString());

            var badgeUpdater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
            badgeUpdater.Update(new BadgeNotification(badgeXml));
        }
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

