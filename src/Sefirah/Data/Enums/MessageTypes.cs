namespace Sefirah.Data.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionType
{
    PlaybackInfo,
    PlaybackUpdate,
    TimelineUpdate,
    RemovedSession
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaybackActionType
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
public enum AudioMessageType
{
    New,
    Removed,
    Active
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommandType
{
    ClearNotifications,
    RequestAppList,
    Disconnect
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
public enum NotificationType
{
    Active,
    Removed,
    New,
    Action,
    Invoke
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConversationType
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
