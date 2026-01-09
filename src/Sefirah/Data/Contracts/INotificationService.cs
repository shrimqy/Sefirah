using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface INotificationService
{
    /// <summary>
    /// Gets notifications for the currently active device session
    /// </summary>
    ReadOnlyObservableCollection<Notification> NotificationHistory { get; }

    /// <summary>
    /// Initializes the notification service
    /// </summary>  
    Task Initialize();

    Task HandleNotificationMessage(PairedDevice device, NotificationMessage notificationMessage);
    void RemoveNotification(PairedDevice device, Notification notification);
    
    /// <summary>
    /// Toggles pin status for a notification in the active session
    /// </summary>
    void TogglePinNotification(Notification notification);
    
    /// <summary>
    /// Clears all notifications for the active session
    /// </summary>
    void ClearAllNotification();
    
    Task ClearHistoryAsync(PairedDevice device);
    void ProcessReplyAction(PairedDevice device, string notificationKey, string replyResultKey, string replyText);
    void ProcessClickAction(PairedDevice device, string notificationKey, int actionIndex);
}
