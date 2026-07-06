using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class NotificationEntity
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public string NotificationKey { get; set; } = string.Empty;

    public bool Pinned { get; set; }

    public string AppPackage { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Text { get; set; }

    public long TimestampMillis { get; set; }

    public string? GroupKey { get; set; }

    public string? Tag { get; set; }

    public string? ReplyResultKey { get; set; }

    public byte[]? LargeIcon { get; set; }

    /// <summary>
    /// JSON for <see cref="NotificationPayload"/> (messages and actions).
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    #region Helpers
    public static string GetKey(string deviceId, string notificationKey) => $"{deviceId}:{notificationKey}";

    public static NotificationEntity FromMessage(NotificationInfo message, string deviceId, bool pinned = false)
    {
        var payload = new NotificationPayload
        {
            Messages = message.Messages,
            Actions = message.Actions,
        };

        byte[]? largeIcon = null;
        try
        {
            largeIcon = Convert.FromBase64String(message.LargeIcon);
        }
        catch (Exception) { }

        return new NotificationEntity
        {
            Key = GetKey(deviceId, message.NotificationKey),
            DeviceId = deviceId,
            NotificationKey = message.NotificationKey,
            Pinned = pinned,
            AppPackage = message.AppPackage ?? string.Empty,
            AppName = message.AppName ?? string.Empty,
            Title = message.Title,
            Text = message.Text,
            TimestampMillis = message.TimestampMillis,
            GroupKey = message.GroupKey,
            Tag = message.Tag,
            ReplyResultKey = message.ReplyResultKey,
            LargeIcon = largeIcon,
            PayloadJson = JsonSerializer.Serialize(payload),
        };
    }

    public Notification ToNotification()
    {
        var payload = DeserializePayload();

        return new()
        {
            DeviceId = DeviceId,
            Key = NotificationKey,
            Pinned = Pinned,
            TimestampMillis = TimestampMillis,
            AppName = AppName,
            AppPackage = AppPackage,
            Title = Title,
            Text = Text,
            GroupedMessages = Notification.GroupBySender(payload.Messages),
            Tag = Tag,
            GroupKey = GroupKey,
            Actions = payload.Actions,
            ReplyResultKey = ReplyResultKey,
            LargeIcon = LargeIcon,
        };
    }

    #endregion

    private NotificationPayload DeserializePayload()
    {
        if (string.IsNullOrEmpty(PayloadJson))
            return new NotificationPayload();

        try
        {
            return JsonSerializer.Deserialize<NotificationPayload>(PayloadJson) ?? new NotificationPayload();
        }
        catch (JsonException)
        {
            return new NotificationPayload();
        }
    }
}
