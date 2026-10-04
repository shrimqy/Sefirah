using Sefirah.Data.Models.Messages;

namespace Sefirah.Utils;

internal static class MessageGrouping
{
    private static readonly long GroupingThreshold = (long)TimeSpan.FromMinutes(5).TotalMilliseconds;

    public static void AddMessage(IList<MessageGroup> groups, Message message)
    {
        if (groups.Count == 0)
        {
            groups.Add(CreateGroup(message));
            return;
        }

        var lastGroup = groups[^1];
        if (message.Timestamp >= lastGroup.LatestTimestamp)
        {
            if (CanGroupWith(message, lastGroup))
                lastGroup.Messages.Add(message);
            else
                groups.Add(CreateGroup(message));
            return;
        }

        var firstGroup = groups[0];
        if (message.Timestamp <= firstGroup.Messages[0].Timestamp)
        {
            if (CanGroupWith(message, firstGroup))
                firstGroup.Messages.Insert(0, message);
            else
                groups.Insert(0, CreateGroup(message));
            return;
        }

        var insertIndex = FindGroupInsertionIndex(groups, message.Timestamp);
        if (!TryAddToExistingGroup(groups, message, insertIndex))
            groups.Insert(insertIndex, CreateGroup(message));
    }

    public static void UpdateMessage(IList<MessageGroup> groups, Message message)
    {
        var group = groups.FirstOrDefault(g => g.Messages.Any(m => m.UniqueId == message.UniqueId));
        if (group is null)
        {
            AddMessage(groups, message);
            return;
        }

        var current = group.Messages.First(m => m.UniqueId == message.UniqueId);
        if (current.Timestamp != message.Timestamp ||
            (current.MessageType == 1) != (message.MessageType == 1) ||
            !PhoneNumberUtils.IsMatch(current.Participant.Address, message.Participant.Address))
        {
            group.Messages.Remove(current);
            if (group.Messages.Count == 0)
                groups.Remove(group);
            AddMessage(groups, message);
        }
        else
        {
            group.Messages[group.Messages.IndexOf(current)] = message;
        }
    }

    public static void RemoveMessages(IList<MessageGroup> groups, IReadOnlySet<long> ids)
    {
        for (var g = groups.Count - 1; g >= 0; g--)
        {
            var group = groups[g];
            for (var m = group.Messages.Count - 1; m >= 0; m--)
            {
                if (ids.Contains(group.Messages[m].UniqueId))
                    group.Messages.RemoveAt(m);
            }
            if (group.Messages.Count == 0)
                groups.RemoveAt(g);
        }
    }

    public static void RefreshHeaders(IEnumerable<MessageGroup> groups)
    {
        DateTime? previousDay = null;
        foreach (var group in groups)
        {
            if (group.Messages.Count == 0)
            {
                group.DateHeader = null;
                group.TimeHeader = null;
                continue;
            }

            var localTime = DateTimeOffset.FromUnixTimeMilliseconds(group.Messages[0].Timestamp).LocalDateTime;
            group.TimeHeader = localTime.ToString("t");
            group.DateHeader = previousDay != localTime.Date ? FormatDayHeader(localTime.Date) : null;
            previousDay = localTime.Date;
        }
    }

    private static MessageGroup CreateGroup(Message message) => new(message.Participant)
    {
        Messages = [message]
    };

    private static int FindGroupInsertionIndex(IList<MessageGroup> groups, long timestamp)
    {
        int left = 0, right = groups.Count;
        while (left < right)
        {
            int mid = left + (right - left) / 2;
            if (groups[mid].Messages[0].Timestamp <= timestamp)
                left = mid + 1;
            else
                right = mid;
        }
        return left;
    }

    private static bool TryAddToExistingGroup(IList<MessageGroup> groups, Message message, int insertIndex)
    {
        if (insertIndex > 0)
        {
            var previous = groups[insertIndex - 1];
            if (CanGroupWith(message, previous))
            {
                InsertMessageSorted(previous, message);
                return true;
            }
        }

        if (insertIndex < groups.Count)
        {
            var next = groups[insertIndex];
            if (CanGroupWith(message, next))
            {
                InsertMessageSorted(next, message);
                return true;
            }
        }

        return false;
    }

    private static void InsertMessageSorted(MessageGroup group, Message message)
    {
        for (var i = 0; i < group.Messages.Count; i++)
        {
            if (message.Timestamp < group.Messages[i].Timestamp)
            {
                group.Messages.Insert(i, message);
                return;
            }
        }
        group.Messages.Add(message);
    }

    private static bool CanGroupWith(Message message, MessageGroup group)
    {
        var isReceived = message.MessageType == 1;
        if (group.IsReceived != isReceived)
            return false;

        if (Math.Abs(message.Timestamp - GetClosestTimestamp(message, group)) > GroupingThreshold)
            return false;

        // Outgoing Participant is the recipient; address formatting differs across local vs phone echoes.
        return !isReceived || PhoneNumberUtils.IsMatch(group.Sender.Address, message.Participant.Address);
    }

    private static long GetClosestTimestamp(Message message, MessageGroup group)
    {
        var firstTimestamp = group.Messages[0].Timestamp;
        var lastTimestamp = group.LatestTimestamp;
        return Math.Abs(message.Timestamp - firstTimestamp) <= Math.Abs(message.Timestamp - lastTimestamp)
            ? firstTimestamp
            : lastTimestamp;
    }

    private static string FormatDayHeader(DateTime day)
    {
        if (day == DateTime.Today)
            return "Today".GetLocalizedResource();

        if (day == DateTime.Today.AddDays(-1))
            return "Yesterday".GetLocalizedResource();

        if (day > DateTime.Today.AddDays(-7))
            return day.ToString("dddd");

        if (day.Year == DateTime.Today.Year)
            return day.ToString("MMMM d");

        return day.ToString("MMMM d, yyyy");
    }
}
