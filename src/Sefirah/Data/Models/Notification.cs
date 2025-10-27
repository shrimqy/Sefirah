using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.Enums;
using Sefirah.Extensions;
using Sefirah.Helpers;

namespace Sefirah.Data.Models;

public class Notification
{
    public string Key { get; set; } = string.Empty;
    public bool Pinned { get; set; } = false;
    public string? TimeStamp { get; set; }
    public NotificationType Type { get; set; }
    public string? AppName { get; set; }
    public string? AppPackage { get; set; }
    public string? Title { get; set; }
    public string? Text { get; set; }
    public List<NotificationGroup>? GroupedMessages { get; set; }
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

    public string FlyoutFilterString => string.Format("NotificationFilterButton".GetLocalizedResource(), AppName);

    #region Helpers
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
            GroupedMessages = GroupBySender(message.Messages),
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

    internal static List<NotificationGroup> GroupBySender(List<NotificationTextMessage>? messages)
    {
        if (messages == null || messages.Count == 0) return [];

        List<NotificationGroup> result = [];
        NotificationGroup? currentGroup = null;

        foreach (var message in messages)
        {
            if (currentGroup?.Sender != message.Sender)
            {
                currentGroup = new NotificationGroup(message.Sender, []);
                result.Add(currentGroup);
            }
            if (!string.IsNullOrEmpty(message.Text))
            {
                currentGroup.Messages.Add(message.Text);
            }
        }
        return result;
    }
    #endregion
}
