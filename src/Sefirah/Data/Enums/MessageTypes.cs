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
public enum ActionType
{
    Lock,
    Shutdown,
    Sleep,
    Hibernate,
    Restart,
    Logoff,
    Custom
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
