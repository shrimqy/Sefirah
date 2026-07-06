using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class CallLogEntity
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public long CallLogId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public long TimestampMillis { get; set; }

    public long DurationSeconds { get; set; }

    public CallLogType CallType { get; set; }

    public static string GetKey(string deviceId, long callLogId) => $"{deviceId}:{callLogId}";

    public static CallLogEntity FromModel(string deviceId, CallLogInfo log) => new()
    {
        Key = GetKey(deviceId, log.CallLogId),
        DeviceId = deviceId,
        CallLogId = log.CallLogId,
        PhoneNumber = log.PhoneNumber,
        TimestampMillis = log.TimestampMillis,
        DurationSeconds = log.DurationSeconds,
        CallType = log.CallType,
    };

    public CallLog ToCallLog(Contact? contact = null)
    {
        return new CallLog(contact ?? new Contact(PhoneNumber))
        {
            CallLogId = CallLogId,
            TimestampMillis = TimestampMillis,
            DurationSeconds = DurationSeconds,
            CallType = CallType,
        };
    }
}
