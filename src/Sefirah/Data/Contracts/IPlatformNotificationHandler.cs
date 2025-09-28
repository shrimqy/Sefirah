using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

/// <summary>
/// Interface for platform-specific notification handlers
/// </summary>
public interface IPlatformNotificationHandler
{
    /// <summary>
    /// Displays a notification from a remote device message
    /// </summary>
    /// <param name="message">The notification message from remote device</param>
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
    Task ShowClipboardNotification(string title, string text, string? iconPath = null);
    
    /// <summary>
    /// Shows a clipboard notification with optional action button
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="text">Notification text</param>
    /// <param name="actionLabel">Label for action button (optional)</param>
    /// <param name="actionData">Data to pass with action (optional)</param>
    /// <returns>Task that completes when the notification is displayed</returns>
    Task ShowClipboardNotificationWithActions(string title, string text, string? actionLabel = null, string? actionData = null);
    
    /// <summary>
    /// Shows a file transfer notification
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="text">Notification text</param>
    /// <param name="filePath">Path to the transferred file (optional)</param>
    /// <param name="folderPath">Path to the folder containing the file (optional)</param>
    /// <returns>Task that completes when the notification is displayed</returns>
    Task ShowFileTransferNotification(string title, string text, string? filePath = null, string? folderPath = null);

    Task ShowTransferNotification(string title, string message, string fileName, uint notificationSequence, double? progress = null, bool silent = false);
    /// <summary>
    /// Registers for platform-specific notification events
    /// </summary>
    Task RegisterForNotifications();
    
    /// <summary>
    /// Removes a notification by its tag/key
    /// </summary>
    /// <param name="notificationKey">The key of the notification to remove</param>
    Task RemoveNotification(string notificationKey);
    
    /// <summary>
    /// Removes all notifications for a specific group
    /// </summary>
    /// <param name="groupKey">The group identifier</param>
    Task RemoveNotificationsByGroup(string groupKey);
    
    /// <summary>
    /// Clears all notifications
    /// </summary>
    Task ClearAllNotifications();
} 
