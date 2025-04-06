using System.Runtime.Serialization;

namespace Sefirah.App.Data.Enums;
public enum FileTransferType
{
    [EnumMember(Value = "BulkFile")]
    BulkFile,
    [EnumMember(Value = "SingleFile")]
    SingleFile,
}

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
    Disconnect,
    Lock,
    Shutdown,
    Sleep,
    Hibernate,
    ClearNotifications,
    ShutdownWithDelay,
    Restart,
    Logoff
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
