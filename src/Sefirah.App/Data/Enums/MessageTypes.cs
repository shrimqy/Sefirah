using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Sefirah.App.Data.Enums;
public enum MessageType
{
    [EnumMember(Value = "0")]
    Misc,
    [EnumMember(Value = "1")]
    DeviceInfo,
    [EnumMember(Value = "2")]
    DeviceStatus,
    [EnumMember(Value = "3")]
    Clipboard,
    [EnumMember(Value = "4")]
    Notification,
    [EnumMember(Value = "5")]
    NotificationAction,
    [EnumMember(Value = "6")]
    ReplyAction,
    [EnumMember(Value = "7")]
    PlaybackData,
    [EnumMember(Value = "8")]
    FileTransferInfo,
    [EnumMember(Value = "9")]
    InteractiveControl,
    [EnumMember(Value = "10")]
    ApplicationInfo,
    [EnumMember(Value = "11")]
    SftpServerInfo,
    [EnumMember(Value = "12")]
    UdpBroadcast
}

public enum InteractiveControlType
{
    [EnumMember(Value = "SINGLE")]
    SingleTapEvent,
    [EnumMember(Value = "HOLD")]
    HoldTapEvent,
    [EnumMember(Value = "SWIPE")]
    SwipeEvent,
    [EnumMember(Value = "KEYBOARD")]
    KeyboardEvent,
    [EnumMember(Value = "SCROLL")]
    ScrollEvent,
    [EnumMember(Value = "KEY")]
    KeyEvent,
}

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

public enum ClipboardType
{
    Text,
    Image
}

public enum ScrollDirection
{
    Up,
    Down
}

public enum KeyboardActionType
{
    Tab, Backspace, Enter, Escape, CtrlC, CtrlV, CtrlX, CtrlA, CtrlZ, CtrlY, Shift
}