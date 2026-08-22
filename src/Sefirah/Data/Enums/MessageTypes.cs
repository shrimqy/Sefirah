namespace Sefirah.Data.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaybackInfoType
{
    PlaybackInfo,
    PlaybackUpdate,
    TimelineUpdate,
    RemovedSession
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MediaActionType
{
    Play,
    Pause,
    Stop,
    Next,
    Previous,
    Seek,
    Shuffle,
    Repeat,
    PlaybackRate,
    VolumeUpdate
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AudioActionType
{
    DefaultDevice,
    VolumeUpdate,
    ToggleMute
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AudioInfoType
{
    New,
    Removed,
    Active
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PowerKind
{
    Lock,
    LogOff,
    Sleep,
    Hibernate,
    Restart,
    Shutdown,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationInfoType
{
    Active,
    Removed,
    New,
    Invoke
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConversationInfoType
{
    Active,
    ActiveUpdated,
    Removed,
    New
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CallState
{
    Ringing,
    InProgress,
    MissedCall
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CallLogType
{
    Incoming,
    Outgoing,
    Missed,
    Voicemail,
    Rejected,
    Blocked,
    AnsweredExternally,
    Unknown,
}

/// <summary>
/// Android AudioManager stream type constants.
/// </summary>
public enum AudioStreamType
{
    VoiceCall = 0,
    Ring = 2,
    Media = 3,
    Alarm = 4,
    Notification = 5
}
