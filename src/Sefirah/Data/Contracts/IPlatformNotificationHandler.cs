using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

/// <summary>
/// Interface for platform-specific notification handlers
/// </summary>
public interface IPlatformNotificationHandler
{
    /// <summary>
    /// Displays a notification from a remote device body
    /// </summary>
    /// <param name="message">The notification body from remote device</param>
    /// <param name="deviceId">The ID of the device that sent the notification</param>
    /// <returns>Task that completes when the notification is displayed</returns>
    Task ShowRemoteNotification(NotificationMessage message, string deviceId);
    
    /// <summary>
    /// Shows a simple notification with title and text
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="text">Notification text</param>
    /// <param name="iconPath">Optional path to icon file</param>
    /// <returns>Task that completes when the notification is displayed</returns>
    void ShowClipboardNotification(string title, string text, string? iconPath = null);
    
    /// <summary>
    /// Shows a clipboard notification with optional action button
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="text">Notification text</param>
    /// <param name="actionLabel">Label for action button (optional)</param>
    /// <param name="actionData">Data to pass with action (optional)</param>
    /// <returns>Task that completes when the notification is displayed</returns>
    void ShowClipboardNotificationWithActions(string title, string text, string? actionLabel = null, string? actionData = null);
    
    /// <summary>
    /// Shows a file transfer notification
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="text">Notification text</param>
    /// <param name="filePath">Path to the transferred file (optional)</param>
    /// <param name="folderPath">Path to the folder containing the file (optional)</param>
    /// <returns>Task that completes when the notification is displayed</returns>
    void ShowCompletedFileTransferNotification(string subtitle, string transferId, string? filePath = null, string? folderPath = null);

    void ShowFileTransferNotification(string subtitle, string fileName, string transferId, uint notificationSequence, double? progress = null);
    /// <summary>
    /// Registers for platform-specific notification events
    /// </summary>
    Task RegisterForNotifications();
    
    /// <summary>
    /// Removes a notification by its tag/key
    /// </summary>
    /// <param name="notificationKey">The key of the notification to remove</param>
    Task RemoveNotificationByTag(string? notificationKey);
    
    /// <summary>
    /// Removes all notifications for a specific group
    /// </summary>
    /// <param name="groupKey">The group identifier</param>
    Task RemoveNotificationsByGroup(string? groupKey);

    Task RemoveNotificationsByTagAndGroup(string? tag, string? groupKey);

    /// <summary>
    /// Clears all notifications
    /// </summary>
    Task ClearAllNotifications();
} 
