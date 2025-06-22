using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils.Serialization;
using Windows.System;

namespace Sefirah.Services;
public class NotificationService(
    ILogger<NotificationService> logger,
    ISessionManager sessionManager,
    IUserSettingsService userSettingsService,
    IDeviceManager deviceManager,
    IPlatformNotificationHandler platformNotificationHandler,
    RemoteAppRepository remoteAppsRepository) : INotificationService
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly Dictionary<string, ObservableCollection<Notification>> deviceNotifications = [];
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher = App.MainWindow!.DispatcherQueue;
    
    private readonly ObservableCollection<Notification> activeNotifications = [];

    /// <summary>
    /// Gets notifications for the currently active device session
    /// </summary>
    public ReadOnlyObservableCollection<Notification> NotificationHistory => new(activeNotifications);

    // Initialize the service - call this after DI container creates the instance
    public void Initialize()
    {
        // Listen to device connection status changes
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        
        ((INotifyPropertyChanged)deviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IDeviceManager.ActiveDevice))
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
            logger.LogDebug("Created new notification collection for device: {DeviceId}", device.Id);
        }
        return notifications;
    }

    private void OnConnectionStatusChanged(object? sender, (PairedDevice Device, bool IsConnected) e)
    {
        if (e.IsConnected)
        {
            // Device connected - clear notification history to prevent duplicates
            logger.LogInformation("Device {DeviceId} connected, clearing notification history to prevent duplicates", e.Device.Id);
            ClearHistory(e.Device);
        }
        else
        {
            // Device disconnected
            logger.LogInformation("Device {DeviceId} disconnected", e.Device.Id);
        }
    }

    private void UpdateActiveNotifications(PairedDevice? activeDevice)
    {
        activeNotifications.Clear();
        
        if (activeDevice != null && deviceNotifications.TryGetValue(activeDevice.Id, out var deviceNotifs))
        {
            foreach (var notification in deviceNotifs)
            {
                activeNotifications.Add(notification);
            }
            logger.LogDebug("Updated active notifications for device {DeviceId}: {Count} notifications", 
                activeDevice.Id, activeNotifications.Count);
        }
        else
        {
            logger.LogDebug("Cleared active notifications - no active device or no notifications for device");
        }
    }

    public async Task HandleNotificationMessage(PairedDevice device, NotificationMessage message)
    {
        // Check if device has notification sync enabled
        if (!device.DeviceSettings.NotificationSyncEnabled)
        {
            logger.LogDebug("Notification sync disabled for device {DeviceId}, skipping notification", device.Id);
            return;
        }

        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            logger.LogDebug("Processing notification message from {AppName} for device {DeviceId}", message.AppName, device.Id);

            if (message.Title != null && message.AppPackage != null)
            {
                var filter = remoteAppsRepository.GetAppNotificationFilter(message.AppPackage, device.Id)
                ?? remoteAppsRepository.AddOrUpdateAppNotificationFilter(device.Id, message.AppPackage, message.AppName!, !string.IsNullOrEmpty(message.AppIcon) ? Convert.FromBase64String(message.AppIcon) : null!);

                if (filter == NotificationFilter.Disabled) return;

                await dispatcher.EnqueueAsync(async () =>
                {
                    var notifications = GetOrCreateNotificationCollection(device);
                    var notification = await Notification.FromMessage(message);
                    
                    if (message.NotificationType == NotificationType.New && filter == NotificationFilter.ToastFeed)
                    {
                        // Check for existing notification in this device's collection
                        var existingNotification = notifications.FirstOrDefault(n => n.Key == notification.Key);

                        if (existingNotification != null)
                        {
                            // Update existing notification
                            var index = notifications.IndexOf(existingNotification);
                            if (existingNotification.IsPinned)
                            {
                                notification.IsPinned = true;
                            }
                            notifications[index] = notification;
                        }
                        else
                        {
                            // Add new notification
                            notifications.Insert(0, notification);
                        }

                        // Show platform-specific notification only if this is the active session
                        if (deviceManager.ActiveDevice?.Id == device.Id)
                        {
                            await platformNotificationHandler.ShowRemoteNotification(message, device.Id);
                        }
                    }
                    else if ((message.NotificationType == NotificationType.Active || message.NotificationType == NotificationType.New)
                        && (filter == NotificationFilter.Feed || filter == NotificationFilter.ToastFeed))
                    {
                        notifications.Add(notification);
                    }
                    else
                    {
                        logger.LogWarning("Notification from {AppName} does not meet criteria for display", message.AppName);
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
                RemoveNotification(device, message.NotificationKey, true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling notification message");
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
            logger.LogError(ex, "Error checking if app '{AppName}' is active", appName);
            return false;
        }
    }

    private void SortNotifications(string deviceId)
    {
        dispatcher.EnqueueAsync(() =>
        {
            if (!deviceNotifications.TryGetValue(deviceId, out var notifications)) return;
            
            var sorted = notifications.OrderByDescending(n => n.IsPinned)
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

    public void RemoveNotification(PairedDevice device, string notificationKey, bool isRemote)
    {
        dispatcher.EnqueueAsync(() =>
        {
            try
            {
                if (!deviceNotifications.TryGetValue(device.Id, out var notifications)) return;
                
                var notification = notifications.FirstOrDefault(n => n.Key == notificationKey);

                if (notification != null)
                {
                    if (!isRemote)
                    {
                        var notificationToRemove = new NotificationMessage
                        {
                            NotificationKey = notificationKey,
                            NotificationType = NotificationType.Removed
                        };  
                        string jsonMessage = SocketMessageSerializer.Serialize(notificationToRemove);
                        if (device.Session != null)
                        {
                            sessionManager.SendMessage(device.Session, jsonMessage);
                            logger.LogDebug("Sent notification removal message to remote device {DeviceId}", device.Id);
                        }
                    }
                    if (!notification.IsPinned || !isRemote)
                    {
                        notifications.Remove(notification);
                        logger.LogDebug("Removed notification with key: {NotificationKey} from device {DeviceId}", notificationKey, device.Id);
                        
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
                logger.LogError(ex, "Error removing notification");
                throw;
            }
        });
    }

    public void TogglePinNotification(string notificationKey)
    {
        var activeDevice = deviceManager.ActiveDevice;
        if (activeDevice == null)
        {
            logger.LogWarning("No active session to toggle pin notification");
            return;
        }

        dispatcher.EnqueueAsync(() =>
        {
            if (!deviceNotifications.TryGetValue(activeDevice.Id, out var notifications)) return;
            
            var notification = notifications.FirstOrDefault(n => n.Key == notificationKey);
            if (notification != null)
            {
                notification.IsPinned = !notification.IsPinned;
                // Update existing notification
                var index = notifications.IndexOf(notification);
                notifications[index] = notification;
                SortNotifications(activeDevice.Id);
                
                // Update active notifications since this is for the active device
                UpdateActiveNotifications(activeDevice);
                
                logger.LogDebug("Pinned status for notification with key: {NotificationKey} is now {IsPinned} for device {DeviceId}", 
                    notificationKey, notification.IsPinned, activeDevice.Id);
            }
        });
    }

    public void ClearAllNotification()
    {
        var activeDevice = deviceManager.ActiveDevice;
        if (activeDevice == null)
        {
            logger.LogWarning("No active session to clear notifications");
            return;
        }

        dispatcher.EnqueueAsync(() =>
        {
            try
            {
                if (deviceNotifications.TryGetValue(activeDevice.Id, out var notifications))
                {
                    notifications.Clear();
                    // Update active notifications since we cleared the active device's notifications
                    UpdateActiveNotifications(activeDevice);
                }
                
                if (activeDevice.Session == null) return;

                var command = new CommandMessage { CommandType = CommandType.ClearNotifications };
                string jsonMessage = SocketMessageSerializer.Serialize(command);
                sessionManager.SendMessage(activeDevice.Session, jsonMessage);
                logger.LogInformation("Cleared all notifications for device {DeviceId}", activeDevice.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error clearing all notifications");
                throw;
            }
        });
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
                    logger.LogInformation("Cleared notification history for device {DeviceId}", device.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error clearing history");
                throw;
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

        if (device.Session == null) return;

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

        if (device.Session == null) return;

        sessionManager.SendMessage(device.Session, SocketMessageSerializer.Serialize(notificationAction));
        logger.LogDebug("Sent click action for notification {NotificationKey} to device {DeviceId}", notificationKey, device.Id);
    }

    /// <summary>
    /// Removes all notifications for a device when it disconnects
    /// </summary>
    public void RemoveDeviceNotifications(string deviceId)
    {
        dispatcher.EnqueueAsync(() =>
        {
            if (deviceNotifications.Remove(deviceId))
            {
                logger.LogInformation("Removed all notifications for disconnected device {DeviceId}", deviceId);
            }
        });
    }
}
