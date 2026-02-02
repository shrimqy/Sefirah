using Sefirah.Data.Models;

namespace Sefirah.Utils;

public static class NotificationActionUtils
{
    public static void ProcessReplyAction(ILogger logger, PairedDevice device, string notificationKey, string replyResultKey, string replyText)
    {
        if (!device.IsConnected) return;

        var replyAction = new NotificationReply
        {
            NotificationKey = notificationKey,
            ReplyResultKey = replyResultKey,
            ReplyText = replyText
        };

        device.SendMessage(replyAction);
        logger.LogDebug("Sent reply action for notification {NotificationKey} to device {DeviceId}", notificationKey, device.Id);
    }

    public static void ProcessClickAction(ILogger logger, PairedDevice device, string notificationKey, int actionIndex)
    {        
        if (!device.IsConnected) return;

        var notificationAction = new NotificationAction
        {
            NotificationKey = notificationKey,
            ActionIndex = actionIndex
        };

        device.SendMessage(notificationAction);
        logger.LogDebug("Sent click action for notification {NotificationKey} to device {DeviceId}", notificationKey, device.Id);
    }
} 
