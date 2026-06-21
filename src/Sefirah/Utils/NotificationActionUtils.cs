using Sefirah.Data.Models;

namespace Sefirah.Utils;

public static class NotificationActionUtils
{
    public static void ProcessReplyAction(PairedDevice device, string notificationKey, string replyResultKey, string replyText)
    {
        if (!device.IsConnected) return;

        var replyAction = new NotificationReply
        {
            NotificationKey = notificationKey,
            ReplyResultKey = replyResultKey,
            ReplyText = replyText
        };

        device.SendMessage(replyAction);
    }

    public static void ProcessClickAction(PairedDevice device, string notificationKey, int actionIndex)
    {        
        if (!device.IsConnected) return;

        var notificationAction = new NotificationAction
        {
            NotificationKey = notificationKey,
            ActionIndex = actionIndex
        };

        device.SendMessage(notificationAction);
    }
} 
