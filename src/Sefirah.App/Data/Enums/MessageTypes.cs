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
public enum MediaAction
{
    Resume,
    Pause,
    NextQueue,
    PrevQueue,
    Seek,
    Volume
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MiscType
{
    Disconnect,
    Lock,
    Shutdown,
    Sleep,
    Hibernate,
    Mirror,
    CloseMirror,
    ClearNotifications
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    Active,
    Removed,
    New,
    Action
}
