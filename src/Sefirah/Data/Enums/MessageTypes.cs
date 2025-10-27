namespace Sefirah.Data.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionType
{
    Session,
    TimelineUpdate,
    PlaybackInfoUpdate,
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
    Removed
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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileTransferType
{
    Clipboard,
    File
}
