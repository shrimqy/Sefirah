using Sefirah.Utils;

namespace Sefirah.Data.Models;

public class Notification
{
    public string DeviceId { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public bool Pinned { get; set; }

    public long TimestampMillis { get; set; }

    public string? AppName { get; set; }

    public string? AppPackage { get; set; }

    public string? Title { get; set; }

    public string? Text { get; set; }

    public List<NotificationGroup> GroupedMessages { get; set; } = [];

    public bool HasGroupedMessages => GroupedMessages?.Count > 0;

    public string? Tag { get; set; }

    public string? GroupKey { get; set; }

    public List<NotificationAction> Actions { get; set; } = [];

    public string? ReplyResultKey { get; set; }

    /// <summary>
    /// Notification-specific image. App icons come from <see cref="AppPackage"/>.
    /// </summary>
    public byte[]? LargeIcon { get; set; }

    public bool HasLargeIcon => LargeIcon is { Length: > 0 };

    public bool HasDisplayIcon => HasLargeIcon
        || (!string.IsNullOrEmpty(AppPackage)
            && !string.IsNullOrEmpty(DeviceId)
            && IconUtils.GetAppIconUri(DeviceId, AppPackage) is not null);

    public bool ShouldShowTitle
    {
        get
        {
            if (GroupedMessages.Count != 0)
            {
                return !string.Equals(Title, GroupedMessages.First().Sender, StringComparison.OrdinalIgnoreCase);
            }

            return !string.Equals(AppName, Title, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string FlyoutFilterString => string.Format("NotificationFilterButton".GetLocalizedResource(), AppName);
    
    #region Helpers
    internal static List<NotificationGroup> GroupBySender(List<NotificationMessage> messages)
    {
        if (messages.Count == 0) return [];

        List<NotificationGroup> groups = [];
        NotificationGroup? currentGroup = null;

        foreach (var message in messages)
        {
            if (currentGroup?.Sender != message.Sender)
            {
                currentGroup = new NotificationGroup(message.Sender, []);
                groups.Add(currentGroup);
            }
            if (!string.IsNullOrEmpty(message.Text))
            {
                currentGroup.Messages.Add(message.Text);
            }
        }
        return groups;
    }
    #endregion
}
