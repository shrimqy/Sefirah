using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ActionInfo), nameof(ActionInfo))]
[JsonDerivedType(typeof(ActionList), nameof(ActionList))]
[JsonDerivedType(typeof(ApplicationInfo), nameof(ApplicationInfo))]
[JsonDerivedType(typeof(ApplicationList), nameof(ApplicationList))]
[JsonDerivedType(typeof(Authentication), nameof(Authentication))]
[JsonDerivedType(typeof(AudioAction), nameof(AudioAction))]
[JsonDerivedType(typeof(AudioDeviceInfo), nameof(AudioDeviceInfo))]
[JsonDerivedType(typeof(AudioStreamState), nameof(AudioStreamState))]
[JsonDerivedType(typeof(BatteryState), nameof(BatteryState))]
[JsonDerivedType(typeof(BluetoothState), nameof(BluetoothState))]
[JsonDerivedType(typeof(CallInfo), nameof(CallInfo))]
[JsonDerivedType(typeof(CallLogInfo), nameof(CallLogInfo))]
[JsonDerivedType(typeof(ClearNotifications), nameof(ClearNotifications))]
[JsonDerivedType(typeof(ClipboardInfo), nameof(ClipboardInfo))]
[JsonDerivedType(typeof(ContactInfo), nameof(ContactInfo))]
[JsonDerivedType(typeof(ConversationInfo), nameof(ConversationInfo))]
[JsonDerivedType(typeof(ConversationsRequest), nameof(ConversationsRequest))]
[JsonDerivedType(typeof(DeviceInfo), nameof(DeviceInfo))]
[JsonDerivedType(typeof(Disconnect), nameof(Disconnect))]
[JsonDerivedType(typeof(DndState), nameof(DndState))]
[JsonDerivedType(typeof(ClipboardTransfer), nameof(ClipboardTransfer))]
[JsonDerivedType(typeof(ShareTransfer), nameof(ShareTransfer))]
[JsonDerivedType(typeof(SmsAttachmentRequest), nameof(SmsAttachmentRequest))]
[JsonDerivedType(typeof(SmsAttachmentTransfer), nameof(SmsAttachmentTransfer))]
[JsonDerivedType(typeof(MediaAction), nameof(MediaAction))]
[JsonDerivedType(typeof(MessageIndex), nameof(MessageIndex))]
[JsonDerivedType(typeof(MessageList), nameof(MessageList))]
[JsonDerivedType(typeof(MessagesRequest), nameof(MessagesRequest))]
[JsonDerivedType(typeof(NotificationAction), nameof(NotificationAction))]
[JsonDerivedType(typeof(NotificationInfo), nameof(NotificationInfo))]
[JsonDerivedType(typeof(NotificationReply), nameof(NotificationReply))]
[JsonDerivedType(typeof(PairMessage), nameof(PairMessage))]
[JsonDerivedType(typeof(BluetoothPairingRequest), nameof(BluetoothPairingRequest))]
[JsonDerivedType(typeof(BluetoothPairingResult), nameof(BluetoothPairingResult))]
[JsonDerivedType(typeof(PlaySound), nameof(PlaySound))]
[JsonDerivedType(typeof(PlaybackInfo), nameof(PlaybackInfo))]
[JsonDerivedType(typeof(RemoveConversation), nameof(RemoveConversation))]
[JsonDerivedType(typeof(RequestApplicationList), nameof(RequestApplicationList))]
[JsonDerivedType(typeof(RequestWorkerLaunch), nameof(RequestWorkerLaunch))]
[JsonDerivedType(typeof(RingerModeState), nameof(RingerModeState))]
[JsonDerivedType(typeof(SftpServerInfo), nameof(SftpServerInfo))]
[JsonDerivedType(typeof(TextMessage), nameof(TextMessage))]
[JsonDerivedType(typeof(UdpBroadcast), nameof(UdpBroadcast))]
public class SocketMessage;

public class Disconnect : SocketMessage;

public class ClearNotifications : SocketMessage;

public class RequestApplicationList : SocketMessage;

public class RequestWorkerLaunch : SocketMessage
{
    public required string Command { get; set; }
}

public class Authentication : SocketMessage
{
    public required string DeviceId { get; set; }

    public required string DeviceName { get; set; }

    public required string PublicKey { get; set; }

    public required string Model { get; set; }
}

public class PairMessage : SocketMessage
{
    public bool Pair { get; set; }
}

public class BluetoothPairingRequest : SocketMessage;

public class BluetoothPairingResult : SocketMessage
{
    public bool Granted { get; set; }
    public string? DeviceName { get; set; }
}

public class UdpBroadcast : SocketMessage
{
    public int Port { get; set; }

    public required string DeviceId { get; set; }

    public required string DeviceName { get; set; }
}

public class DeviceInfo : SocketMessage
{
    public required string DeviceName { get; set; }

    public string? Avatar { get; set; } = null;

    public List<PhoneNumber> PhoneNumbers { get; set; } = [];
}

public class BatteryState : SocketMessage
{
    public int BatteryLevel { get; set; }

    public bool IsCharging { get; set; }
}

public class RingerModeState : SocketMessage
{
    public int Mode { get; set; }
}

public class DndState : SocketMessage
{
    public bool IsEnabled { get; set; }
}

public class BluetoothState : SocketMessage
{
    public bool IsEnabled { get; set; }
}

public class CallInfo : SocketMessage
{
    public CallState CallState { get; set; }

    public required string PhoneNumber { get; set; }

    public ContactInfo? ContactInfo { get; set; }
}

public class CallLogInfo : SocketMessage
{
    public long CallLogId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public long TimestampMillis { get; set; }

    public long DurationSeconds { get; set; }

    public CallLogType CallType { get; set; }

    public ContactInfo? ContactInfo { get; set; }
}

public class AudioStreamState : SocketMessage
{
    public AudioStreamType StreamType { get; set; }

    public int Level { get; set; }
}

public class AudioDeviceInfo : SocketMessage
{
    public AudioInfoType InfoType { get; set; }

    public required string DeviceId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public float Volume { get; set; }

    public bool IsMuted { get; set; }

    public bool IsSelected { get; set; }
}

/// <summary>New/changed thread row: recipients, checksum, and tip message for the sidebar.</summary>
public class ConversationInfo : SocketMessage
{
    public required long ThreadId { get; set; }

    public List<string> Recipients { get; set; } = [];

    public long Checksum { get; set; }

    public required TextMessage Message { get; set; }
}

/// <summary>Message bodies. MessageIds is the full thread inventory on Open only.</summary>
public class MessageList : SocketMessage
{
    public required long ThreadId { get; set; }

    public required MessageListKind Kind { get; set; }

    public List<TextMessage> Messages { get; set; } = [];

    /// <summary>Full thread inventory on Open only, newest first.</summary>
    public List<long>? MessageIds { get; set; }

    /// <summary>Thread checksum after this change; New only.</summary>
    public long? Checksum { get; set; }
}

/// <summary>Authoritative message-id set for a thread after a delete, plus checksum.</summary>
public class MessageIndex : SocketMessage
{
    public required long ThreadId { get; set; }

    public List<long> MessageIds { get; set; } = [];

    public long Checksum { get; set; }
}

/// <summary>Thread removed on the phone.</summary>
public class RemoveConversation : SocketMessage
{
    public required long ThreadId { get; set; }
}

/// <summary>Desktop -> phone: null MessageIds = Open (tip page + index); set = History bodies for these ids.</summary>
public class MessagesRequest : SocketMessage
{
    public required long ThreadId { get; set; }

    public List<long>? MessageIds { get; set; }
}

public class TextMessage : SocketMessage
{
    public long UniqueId { get; set; }

    public List<string> Addresses { get; set; } = [];

    public required string Body { get; set; }

    public long Timestamp { get; set; }

    public int MessageType { get; set; }

    public bool Read { get; set; } = false;

    public int SubscriptionId { get; set; } = 0;

    public List<SmsAttachment>? Attachments { get; set; } = null;

    /// <summary>Desktop outbound UniqueId; set on phone tips after sent-intent maps Telephony _id.</summary>
    public long? TempId { get; set; }
}

public class SmsAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string? Thumbnail { get; set; }
    public long PartId { get; set; } = -1;
    public string? Base64EncodedFile { get; set; }
}

public class SmsAttachmentRequest : SocketMessage
{
    public long PartId { get; set; }
}

/// <summary>Desktop → phone on connect: known thread ids + checksums for cheap tip sync.</summary>
public class ConversationsRequest : SocketMessage
{
    public List<ThreadInfo> KnownThreads { get; set; } = [];
}

public class ThreadInfo
{
    public long ThreadId { get; set; }

    public long Checksum { get; set; }
}

public class ContactInfo : SocketMessage
{
    public string Id { get; set; }

    public required string LookupKey { get; set; }

    public required string DisplayName { get; set; }

    public required string Number { get; set; }

    public string PhotoBase64 { get; set; } = string.Empty;
}

// --- Notifications ---
public class NotificationInfo : SocketMessage
{
    public required string NotificationKey { get; set; }

    public NotificationInfoType InfoType { get; set; }

    public long TimestampMillis { get; set; }

    public string? AppPackage { get; set; }

    public string? AppName { get; set; }

    public string? Title { get; set; }

    public string? Text { get; set; }

    public List<NotificationMessage> Messages { get; set; } = [];

    public string? GroupKey { get; set; }

    public string? Tag { get; set; }

    public List<NotificationAction> Actions { get; set; } = [];

    public string? ReplyResultKey { get; set; }

    public string? AppIcon { get; set; }

    public string LargeIcon { get; set; } = string.Empty;
}

public record NotificationMessage(string Sender, string Text);

public class NotificationAction : SocketMessage
{
    public string? NotificationKey { get; set; }

    public string? Label { get; set; } = string.Empty;

    public int ActionIndex { get; set; }
}

public class NotificationReply : SocketMessage
{
    public required string NotificationKey { get; set; }

    public required string ReplyResultKey { get; set; }

    public required string ReplyText { get; set; }
}

public class FileTransferSession
{
    public required List<FileMetadata> Files { get; set; }

    public required ServerInfo ServerInfo { get; set; }
}

public class ShareTransfer : SocketMessage
{
    public required FileTransferSession Transfer { get; set; }
}

public class ClipboardTransfer : SocketMessage
{
    public required FileTransferSession Transfer { get; set; }
}

public class SmsAttachmentTransfer : SocketMessage
{
    public required FileTransferSession Transfer { get; set; }

    public long PartId { get; set; }
}

public class SftpServerInfo : SocketMessage
{
    public required string Username { get; set; }

    public required string Password { get; set; }

    public int Port { get; set; }

    public List<string> Paths { get; set; } = [];

    public List<string> PathNames { get; set; } = [];
}

public class ClipboardInfo : SocketMessage
{
    public required string ClipboardType { get; set; }

    public required string Content { get; set; }
}

public class PlaybackInfo : SocketMessage
{
    public PlaybackInfoType InfoType { get; set; }

    public required string Source { get; set; }

    public string? TrackTitle { get; set; }

    public string? Artist { get; set; }

    public bool IsPlaying { get; set; }

    public bool? IsShuffleActive { get; set; }

    public int? RepeatMode { get; set; }

    public double? PlaybackRate { get; set; }

    public double? Position { get; set; }

    public double? MaxSeekTime { get; set; }

    public double? MinSeekTime { get; set; }

    public string? Thumbnail { get; set; }

    public string? AppName { get; set; }

    public int Volume { get; set; }

    public bool? CanPlay { get; set; }

    public bool? CanPause { get; set; }

    public bool? CanGoNext { get; set; }

    public bool? CanGoPrevious { get; set; }

    public bool? CanSeek { get; set; }
}

public class MediaAction : SocketMessage
{
    public MediaActionType ActionType { get; set; }

    public required string Source { get; set; }

    public double? Value { get; set; }
}

public class AudioAction : SocketMessage
{
    public AudioActionType ActionType { get; set; }

    public required string Source { get; set; }

    public double? Value { get; set; }
}

public class PlaySound : SocketMessage
{
    public bool IsPlaying { get; set; }
}

public class ApplicationList : SocketMessage
{
    public required List<ApplicationInfo> AppList { get; set; }
}

public class ApplicationInfo : SocketMessage
{
    public required string PackageName { get; set; }

    public required string AppName { get; set; }

    public string? AppIcon { get; set; }
}

public class ActionInfo : SocketMessage
{
    public required string ActionId { get; set; }

    public required string ActionName { get; set; }

    public string? Icon { get; set; }

    public bool AskForConfirmation { get; set; }
}

public class ActionList : SocketMessage
{
    public List<ActionInfo> Actions { get; set; } = [];
}
