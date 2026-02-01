using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class NotificationEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public bool Pinned { get; set; }

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Serialized NotificationMessage
    /// </summary>
    public string NotificationMessage { get; set; } = string.Empty;

    /// <summary>
    /// Deserializes NotificationMessage and builds Notification for UI
    /// </summary>
    internal async Task<Notification?> ToNotificationAsync()
    {
        var message = JsonSerializer.Deserialize<NotificationMessage>(NotificationMessage);
        if (message is null) return null;

        var notification = await Notification.FromMessage(message);
        if (notification is null) return null;

        notification.Pinned = Pinned;
        return notification;
    }
}
