using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class CallLogEntity
{
    [PrimaryKey]
    public string LogKey { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public long CallLogId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public long TimestampMillis { get; set; }

    public long DurationSeconds { get; set; }

    public CallLogType CallType { get; set; }

    public static CallLogEntity FromModel(string deviceId, CallLogInfo log)
    {
        return new CallLogEntity
        {
            LogKey = $"{deviceId}:id:{log.CallLogId}",
            DeviceId = deviceId,
            CallLogId = log.CallLogId,
            PhoneNumber = log.PhoneNumber,
            TimestampMillis = log.TimestampMillis,
            DurationSeconds = log.DurationSeconds,
            CallType = log.CallType,
        };
    }

    public CallLog ToCallLogAsync(CallerContact? contact = null)
    {
        var displayName = contact is null || string.IsNullOrWhiteSpace(contact.DisplayName)
            ? PhoneNumber
            : contact.DisplayName;

        return new CallLog
        {
            CallLogId = CallLogId,
            PhoneNumber = PhoneNumber,
            TimestampMillis = TimestampMillis,
            DurationSeconds = DurationSeconds,
            CallType = CallType,
            DisplayName = displayName,
            AvatarImage = contact?.Avatar,
        };
    }
}
