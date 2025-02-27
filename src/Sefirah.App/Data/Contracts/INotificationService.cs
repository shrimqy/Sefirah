using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;
public interface INotificationService
{
    /// <summary>
    /// Gets the notification history.
    /// </summary>
    ReadOnlyObservableCollection<Notification> NotificationHistory { get; }

    event EventHandler<Notification> NotificationReceived;

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
    /// Toggles the pin status of a notification by its notificationKey.
    /// </summary>
    /// <param name="notificationKey">The key of the notification to toggle the pin status of.</param>
    Task TogglePinNotification(string notificationKey);

    /// <summary>
    /// Processes a reply action.
    /// </summary>
    /// <param name="notificationKey">The key of the notification.</param>
    /// <param name="replyResultKey">The key of the reply result.</param>
    /// <param name="replyText">The text of the reply.</param>
    void ProcessReplyAction(string notificationKey, string replyResultKey, string replyText);

    /// <summary>
    /// Processes a click action.
    /// </summary>
    /// <param name="notificationKey">The key of the notification.</param>
    /// <param name="actionIndex">The index of the action.</param>
    void ProcessClickAction(string notificationKey, int actionIndex);

    /// <summary>
    /// Clears all notifications.
    /// </summary>
    Task ClearAllNotification();

    /// <summary>
    /// Clears all notifications.
    /// </summary>
    Task ClearHistory();
}
