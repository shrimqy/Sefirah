using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils.Serialization;
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
    private readonly Dictionary<string, ObservableCollection<Notification>> deviceNotifications = [];
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher = App.MainWindow.DispatcherQueue;
    
    private readonly ObservableCollection<Notification> activeNotifications = [];

    /// <summary>
    /// Gets notifications for the currently active device session
    /// </summary>
    public ReadOnlyObservableCollection<Notification> NotificationHistory => new(activeNotifications);

    // Initialize the service - call this after DI container creates the instance
    public void Initialize()
    {
        ClearBadge();
        // Listen to device connection status changes
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        
        ((INotifyPropertyChanged)deviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(IDeviceManager.ActiveDevice) && deviceManager.ActiveDevice is not null)
                UpdateActiveNotifications(deviceManager.ActiveDevice);
        };
    }

    /// <summary>
    /// Gets or creates the notification collection for a device session
    /// </summary>
    private ObservableCollection<Notification> GetOrCreateNotificationCollection(PairedDevice device)
    {
        if (!deviceNotifications.TryGetValue(device.Id, out var notifications))
        {
            notifications = [];
            deviceNotifications[device.Id] = notifications;
        }
        return notifications;
    }

    private void OnConnectionStatusChanged(object? sender, (PairedDevice Device, bool IsConnected) e)
    {
        if (e.IsConnected)
        {
            ClearHistory(e.Device);
        }
    }

    public async Task HandleNotificationMessage(PairedDevice device, NotificationMessage message)
    {
        // Check if device has notification sync enabled
        if (!device.DeviceSettings.NotificationSyncEnabled) return;
        
        try
        { 
            if (message.Title is not null && message.AppPackage is not null)
            {
                var filter = remoteAppsRepository.GetAppNotificationFilterAsync(message.AppPackage, device.Id)
                ?? await remoteAppsRepository.AddOrUpdateApplicationForDevice(device.Id, message.AppPackage, message.AppName!, message.AppIcon);

                if (filter == NotificationFilter.Disabled) return;

                await dispatcher.EnqueueAsync(async () =>
                {
                    var notifications = GetOrCreateNotificationCollection(device);
                    var notification = await Notification.FromMessage(message);
                    
                    if (message.NotificationType == NotificationType.New && filter == NotificationFilter.ToastFeed)
                    {
                        // Check for existing notification in this device's collection
                        var existingNotification = notifications.FirstOrDefault(n => n.Key == notification.Key);

                        if (existingNotification is not null)
                        {
                            // Update existing notification
                            var index = notifications.IndexOf(existingNotification);
                            if (existingNotification.Pinned)
                            {
                                notification.Pinned = true;
                            }
                            notifications[index] = notification;
                        }
                        else
                        {
                            // Add new notification
                            notifications.Insert(0, notification);
                        }
#if WINDOWS
                        // Check if the app is active before showing the notification
                        if (device.DeviceSettings.IgnoreWindowsApps && await IsAppActiveAsync(message.AppName!)) return;
#endif
                        await platformNotificationHandler.ShowRemoteNotification(message, device.Id);
                    }
                    else if ((message.NotificationType is NotificationType.Active || message.NotificationType is NotificationType.New)
                        && (filter is NotificationFilter.Feed || filter is NotificationFilter.ToastFeed))
                    {
                        notifications.Add(notification);
                    }
                    else
                    {
                        return;
                    }
                    
                    SortNotifications(device.Id);
                    
                    // Update active notifications if this is for the active device
                    if (deviceManager.ActiveDevice?.Id == device.Id)
                    {
                        UpdateActiveNotifications(deviceManager.ActiveDevice);
                    }
                });
            }
            else if (message.NotificationType == NotificationType.Removed)
            {
                if (!deviceNotifications.TryGetValue(device.Id, out var notifications)) return;
                var notification = notifications.FirstOrDefault(n => n.Key == message.NotificationKey);
                if (notification is not null && !notification.Pinned)
                {
                    await dispatcher.EnqueueAsync(() => notifications.Remove(notification));
                    // Update active notifications if this is for the active device
                    if (deviceManager.ActiveDevice?.Id == device.Id)
                    {
                        UpdateActiveNotifications(deviceManager.ActiveDevice);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling notification message");
        }
    }

    public void RemoveNotification(PairedDevice device, Notification notification)
    {
        try
        {
            if (!deviceNotifications.TryGetValue(device.Id, out var notifications)) return;
                
            if (!notification.Pinned)
            {
                dispatcher.EnqueueAsync(() => notifications.Remove(notification));
                logger.LogDebug("Removed notification with key: {NotificationKey} from device {DeviceId}", notification, device.Id);

                platformNotificationHandler.RemoveNotificationsByTagAndGroup(notification.Tag, notification.GroupKey);

                // Update active notifications if this is for the active device
                if (deviceManager.ActiveDevice?.Id == device.Id)
                {   
                    UpdateActiveNotifications(deviceManager.ActiveDevice);
                }

                var notificationToRemove = new NotificationMessage
                {
                    NotificationKey = notification.Key,
                    NotificationType = NotificationType.Removed
                };
                string jsonMessage = SocketMessageSerializer.Serialize(notificationToRemove);
                if (device.Session is not null)
                {
                    sessionManager.SendMessage(device.Session, jsonMessage);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing notification");
        }
    }

    public void TogglePinNotification(Notification notification)
    {
        var activeDevice = deviceManager.ActiveDevice!;

        dispatcher.EnqueueAsync(() =>
        {
            if (!deviceNotifications.TryGetValue(activeDevice.Id, out var notifications)) return;
            
            notification.Pinned = !notification.Pinned;
            // Update existing notification
            var index = notifications.IndexOf(notification);
            notifications[index] = notification;
            SortNotifications(activeDevice.Id);
                
            // Update active notifications since this is for the active device
            UpdateActiveNotifications(activeDevice);
        });
    }

    public void ClearAllNotification()
    {
        var activeDevice = deviceManager.ActiveDevice!;
        try
        {
            ClearHistory(activeDevice);
            if (activeDevice.Session is null) return;

            var command = new CommandMessage { CommandType = CommandType.ClearNotifications };
            string jsonMessage = SocketMessageSerializer.Serialize(command);
            sessionManager.SendMessage(activeDevice.Session, jsonMessage);
            logger.LogInformation("Cleared all notifications for device {DeviceId}", activeDevice.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing all notifications");
        }
    }

    public void ClearHistory(PairedDevice device)
    {
        dispatcher.EnqueueAsync(() =>
        {
            try
            {
                if (deviceNotifications.TryGetValue(device.Id, out var notifications))
                {
                    notifications.Clear();
                    ClearBadge();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error clearing history");
            }
        });
    }

    private void SortNotifications(string deviceId)
    {
        dispatcher.EnqueueAsync(() =>
        {
            if (!deviceNotifications.TryGetValue(deviceId, out var notifications)) return;

            var sorted = notifications.OrderByDescending(n => n.Pinned)
                .ThenByDescending(n => n.TimeStamp)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                int currentIndex = notifications.IndexOf(sorted[i]);
                if (currentIndex != i)
                {
                    notifications.Move(currentIndex, i);
                }
            }
        });
    }

    public void ProcessReplyAction(PairedDevice device, string notificationKey, string ReplyResultKey, string replyText)
    {
        var replyAction = new ReplyAction
        {
            NotificationKey = notificationKey,
            ReplyResultKey = ReplyResultKey,
            ReplyText = replyText,
        };

        if (device.Session is null) return;

        sessionManager.SendMessage(device.Session, SocketMessageSerializer.Serialize(replyAction));
        logger.LogDebug("Sent reply action for notification {NotificationKey} to device {DeviceId}", notificationKey, device.Id);
    }

    public void ProcessClickAction(PairedDevice device, string notificationKey, int actionIndex)
    {
        var notificationAction = new NotificationAction
        {
            NotificationKey = notificationKey,
            ActionIndex = actionIndex,
            IsReplyAction = false
        };

        if (device.Session is null) return;

        sessionManager.SendMessage(device.Session, SocketMessageSerializer.Serialize(notificationAction));
        logger.LogDebug("Sent click action for notification {NotificationKey} to device {DeviceId}", notificationKey, device.Id);
    }

    private void UpdateActiveNotifications(PairedDevice activeDevice)
    {
        dispatcher.EnqueueAsync(() =>
        {
            activeNotifications.Clear();

            if (deviceNotifications.TryGetValue(activeDevice.Id, out var deviceNotifs))
            {
                activeNotifications.AddRange(deviceNotifs);

                if (activeDevice.DeviceSettings.ShowBadge)
                {
                    // Get the blank badge XML payload for a badge number
                    XmlDocument badgeXml =
                        BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);

                    // Set the value of the badge in the XML to our number
                    XmlElement badgeElement = badgeXml.SelectSingleNode("/badge") as XmlElement;
                    badgeElement.SetAttribute("value", deviceNotifs.Count.ToString());

                    // Create the badge notification
                    BadgeNotification badge = new(badgeXml);

                    // Create the badge updater for the application
                    BadgeUpdater badgeUpdater =
                        BadgeUpdateManager.CreateBadgeUpdaterForApplication();

                    // And update the badge
                    badgeUpdater.Update(badge);
                }

            }
        });
    }

    /// <summary>
    /// Clears the badge number on the app tile
    /// </summary>
    private void ClearBadge()
    {
        try
        {
            dispatcher.EnqueueAsync(() =>
            {
                BadgeUpdater badgeUpdater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
                badgeUpdater.Clear();
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing badge number at startup");
        }
    }

#if WINDOWS
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
            logger.LogError(ex, "Error checking if app '{AppName}' is active", appName);
            return false;
        }
    }
#endif
}
