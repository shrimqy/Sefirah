using System.Runtime.Serialization;

namespace Sefirah.App.Data.Enums;
public enum FileTransferType
{
    [EnumMember(Value = "BulkFile")]
    BulkFile,
    [EnumMember(Value = "SingleFile")]
    SingleFile,
}

public enum MediaAction
{
    Resume,
    Pause,
    NextQueue,
    PrevQueue,
    Seek,
    Volume
}

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

public enum NotificationType
{
    Active,
    Removed,
    New,
    Action
}
