using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Microsoft.Extensions.Logging;

namespace Sefirah.Platforms.Desktop;

/// <summary>
/// Desktop implementation of the platform notification handler
/// </summary>
public class DesktopNotificationHandler : IPlatformNotificationHandler
{
    private readonly ILogger<DesktopNotificationHandler> _logger;

    public DesktopNotificationHandler(ILogger<DesktopNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task ShowRemoteNotification(NotificationMessage message, string deviceId)
    {
        return Task.CompletedTask;
    }

    public Task ShowSimpleNotification(string title, string text, string? iconPath = null)
    {
        return Task.CompletedTask;
    }

    public Task ShowClipboardNotification(string title, string text, string? actionLabel = null, string? actionData = null)
    {
        return Task.CompletedTask;
    }

    public Task ShowFileTransferNotification(string title, string text, string? filePath = null, string? folderPath = null)
    {
        return Task.CompletedTask;
    }

    public Task ShowTransferNotification(string title, string message, string fileName, uint notificationSequence, double? progress = null, bool isReceiving = true, bool silent = false)
    {
        return Task.CompletedTask;
    }

    public Task RegisterForNotifications()
    {
        return Task.CompletedTask;
    }

    public Task RemoveNotification(string notificationKey)
    {
        return Task.CompletedTask;
    }

    public Task RemoveNotificationsByGroup(string groupKey)
    {
        return Task.CompletedTask;
    }

    public Task ClearAllNotifications()
    {
        return Task.CompletedTask;
    }
}
