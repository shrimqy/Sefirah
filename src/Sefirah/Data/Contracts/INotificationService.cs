using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface INotificationService
{
    /// <summary>
    /// Initializes the notification service
    /// </summary>  
    Task Initialize();

    Task HandleNotificationMessage(PairedDevice device, NotificationInfo notificationMessage);

    /// <summary>
    /// Single collection for the active device's notifications.
    /// </summary>
    ObservableCollection<Notification> Notifications { get; }

    void RemoveNotification(PairedDevice device, Notification notification);
    
    /// <summary>
    /// Toggles pin status for a notification in the active session
    /// </summary>
    void TogglePinNotification(Notification notification);
    
    /// <summary>
    /// Clears all notifications for the active session
    /// </summary>
    void ClearAllNotification();
    
    void ProcessReplyAction(PairedDevice device, string notificationKey, string replyResultKey, string replyText);
    void ProcessClickAction(PairedDevice device, string notificationKey, int actionIndex);
}
