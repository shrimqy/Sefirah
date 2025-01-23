using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;
public interface INotificationService
{
    /// <summary>
    /// Gets the notification history.
    /// </summary>
    ReadOnlyObservableCollection<NotificationMessage> NotificationHistory { get; }

    event EventHandler<NotificationMessage> NotificationReceived;

    /// <summary>
    /// Handles incoming notification messages. 
    /// </summary>
    /// <param name="message">The notification message to handle.</param>
    Task HandleNotificationMessage(NotificationMessage message);

    /// <summary>
    /// Removes a notification by its notificationKey.
    /// </summary>
    /// <param name="notificationKey">The key of the notification to remove.</param>
    /// <param name="isRemote">Indicates if the notification is incoming from the remote device.</param>
    Task RemoveNotification(string notificationKey, bool isRemote);

    /// <summary>
    /// Clears all notifications.
    /// </summary>
    Task ClearAllNotification();

    /// <summary>
    /// Clears all notifications.
    /// </summary>
    Task ClearHistory();
}
