using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Utils.Serialization;

namespace Sefirah.Utils;

public static class NotificationActionUtils
{
    public static void ProcessReplyAction(ISessionManager sessionManager, ILogger logger, PairedDevice device, string notificationKey, string replyResultKey, string replyText)
    {
        var replyAction = new ReplyAction
        {
            NotificationKey = notificationKey,
            ReplyResultKey = replyResultKey,
            ReplyText = replyText,
        };

        if (device.Session == null) return;

        sessionManager.SendMessage(device.Session, SocketMessageSerializer.Serialize(replyAction));
        logger.LogDebug("Sent reply action for notification {NotificationKey} to device {DeviceId}", notificationKey, device.Id);
    }

    public static void ProcessClickAction(ISessionManager sessionManager, ILogger logger, PairedDevice device, string notificationKey, int actionIndex)
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
} 