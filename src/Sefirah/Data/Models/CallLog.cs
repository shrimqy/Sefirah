namespace Sefirah.Data.Models;

public partial class CallLog(Contact callContact) : ObservableObject
{
    public Contact CallContact { get; } = callContact;

    public long CallLogId { get; set; }

    public long TimestampMillis { get; set; }

    public long DurationSeconds { get; set; }

    public CallLogType CallType { get; set; }

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public DateTimeOffset LocalTimestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampMillis).ToLocalTime();

    public string FormattedDate => LocalTimestamp.ToString("d");

    public string FormattedTime => LocalTimestamp.ToString("h:mm tt");

    public string FormattedWhen => $"{FormattedDate} at {FormattedTime}";

    public string FormattedDuration
    {
        get
        {
            if (DurationSeconds <= 0)
            {
                return string.Empty;
            }

            var ts = TimeSpan.FromSeconds(DurationSeconds);
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
            }

            if (ts.TotalMinutes >= 1)
            {
                return $"{ts.Minutes}m {ts.Seconds}s";
            }

            return $"{ts.Seconds}s";
        }
    }

    public string CallTypeAndDuration => string.IsNullOrWhiteSpace(FormattedDuration) ? $"{CallType}" : $"{CallType} · {FormattedDuration}";
}
