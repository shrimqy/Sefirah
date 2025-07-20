using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.Enums;
using Sefirah.Extensions;
using Sefirah.Helpers;

namespace Sefirah.Data.Models;

public class Notification
{
    public string Key { get; set; } = string.Empty;
    public bool IsPinned { get; set; } = false;
    public string? TimeStamp { get; set; }
    public NotificationType Type { get; set; }
    public string? AppName { get; set; }
    public string? AppPackage { get; set; }
    public string? Title { get; set; }
    public string? Text { get; set; }
    public List<GroupedNotificationTextMessage>? GroupedMessages { get; set; }
    public bool HasGroupedMessages => GroupedMessages?.Count > 0;
    public string? Tag { get; set; }
    public string? GroupKey { get; set; }
    public List<NotificationAction> Actions { get; set; } = [];
    public string? ReplyResultKey { get; set; }
    public BitmapImage? Icon { get; set; }
    public string? DeviceId { get; set; }

    public bool ShouldShowTitle
    {
        get
        {
            if (GroupedMessages != null && GroupedMessages.Count != 0)
            {
                return !string.Equals(Title, GroupedMessages.First().Sender, StringComparison.OrdinalIgnoreCase);
            }

            return !string.Equals(AppName, Title, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string? FlyoutFilterString => AppName != null 
        ? string.Format(
            "NotificationFilterButton".GetLocalizedResource(),
            AppName)
        : null;

    public static async Task<Notification> FromMessage(NotificationMessage message)
    {
        var notification = new Notification
        {
            Key = message.NotificationKey,
            TimeStamp = message.TimeStamp,
            Type = message.NotificationType,
            AppName = message.AppName,
            AppPackage = message.AppPackage,
            Title = message.Title,
            Text = message.Text,
            GroupedMessages = message.Messages?.GroupBySender(),
            Tag = message.Tag,
            GroupKey = message.GroupKey,
            Actions = message.Actions.Where(a => a != null).ToList()!,
            ReplyResultKey = message.ReplyResultKey
        };

        // Handle icon conversion
        if (!string.IsNullOrEmpty(message.LargeIcon))
        {
            notification.Icon = await Convert.FromBase64String(message.LargeIcon).ToBitmapAsync();
        }
        else if (!string.IsNullOrEmpty(message.AppIcon))
        {
            notification.Icon = await Convert.FromBase64String(message.AppIcon).ToBitmapAsync();
        }
        return notification;
    }
}

// Helper extension method for grouping messages
public static class NotificationExtensions
{
    public static List<GroupedNotificationTextMessage> GroupBySender(this List<NotificationTextMessage> messages)
    {
        var result = new List<GroupedNotificationTextMessage>();
        var currentGroup = new GroupedNotificationTextMessage();

        foreach (var message in messages)
        {
            if (currentGroup.Sender != message.Sender)
            {
                if (currentGroup.Sender != null)
                {
                    result.Add(currentGroup);
                }
                currentGroup = new GroupedNotificationTextMessage
                {
                    Sender = message.Sender,
                    Messages = []
                };
            }
            currentGroup.Messages.Add(message.Text);
        }

        if (currentGroup.Sender != null)
        {
            result.Add(currentGroup);
        }

        return result;
    }
}
