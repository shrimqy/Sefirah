using Sefirah.Data.Enums;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ActionInfo), nameof(ActionInfo))]
[JsonDerivedType(typeof(ApplicationInfo), nameof(ApplicationInfo))]
[JsonDerivedType(typeof(ApplicationList), nameof(ApplicationList))]
[JsonDerivedType(typeof(Authentication), nameof(Authentication))]
[JsonDerivedType(typeof(AudioDeviceInfo), nameof(AudioDeviceInfo))]
[JsonDerivedType(typeof(AudioStreamState), nameof(AudioStreamState))]
[JsonDerivedType(typeof(BatteryState), nameof(BatteryState))]
[JsonDerivedType(typeof(ClearNotifications), nameof(ClearNotifications))]
[JsonDerivedType(typeof(ClipboardInfo), nameof(ClipboardInfo))]
[JsonDerivedType(typeof(ConnectionAck), nameof(ConnectionAck))]
[JsonDerivedType(typeof(ContactInfo), nameof(ContactInfo))]
[JsonDerivedType(typeof(ConversationInfo), nameof(ConversationInfo))]
[JsonDerivedType(typeof(DeviceInfo), nameof(DeviceInfo))]
[JsonDerivedType(typeof(Disconnect), nameof(Disconnect))]
[JsonDerivedType(typeof(DndState), nameof(DndState))]
[JsonDerivedType(typeof(FileTransferInfo), nameof(FileTransferInfo))]
[JsonDerivedType(typeof(MediaAction), nameof(MediaAction))]
[JsonDerivedType(typeof(NotificationAction), nameof(NotificationAction))]
[JsonDerivedType(typeof(NotificationInfo), nameof(NotificationInfo))]
[JsonDerivedType(typeof(NotificationReply), nameof(NotificationReply))]
[JsonDerivedType(typeof(PairMessage), nameof(PairMessage))]
[JsonDerivedType(typeof(PlaybackInfo), nameof(PlaybackInfo))]
[JsonDerivedType(typeof(RequestApplicationList), nameof(RequestApplicationList))]
[JsonDerivedType(typeof(RingerModeState), nameof(RingerModeState))]
[JsonDerivedType(typeof(SftpServerInfo), nameof(SftpServerInfo))]
[JsonDerivedType(typeof(TextMessage), nameof(TextMessage))]
[JsonDerivedType(typeof(ThreadRequest), nameof(ThreadRequest))]
[JsonDerivedType(typeof(UdpBroadcast), nameof(UdpBroadcast))]
public class SocketMessage;

public class ConnectionAck : SocketMessage;

public class Disconnect : SocketMessage;

public class ClearNotifications : SocketMessage;

public class RequestApplicationList : SocketMessage;

public class Authentication : SocketMessage
{
    public required string DeviceId { get; set; }

    public required string DeviceName { get; set; }

    public required string PublicKey { get; set; }

    public required string Nonce { get; set; }

    public required string Proof { get; set; }

    public required string Model { get; set; }
}

public class PairMessage : SocketMessage
{
    public bool Pair { get; set; }
}

public class UdpBroadcast : SocketMessage
{
    public int Port { get; set; }

    public required string DeviceId { get; set; }

    public required string DeviceName { get; set; }

    public required string PublicKey { get; set; }
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

public class ConversationInfo : SocketMessage
{
    public required ConversationInfoType InfoType { get; set; }

    public required long ThreadId { get; set; }

    public List<string> Recipients { get; set; } = [];

    public List<TextMessage> Messages { get; set; } = [];
}

public class TextMessage : SocketMessage
{
    public long UniqueId { get; set; }

    public List<string> Addresses { get; set; } = [];

    public long ThreadId { get; set; }

    public required string Body { get; set; }

    public long Timestamp { get; set; }

    public int MessageType { get; set; }

    public bool Read { get; set; } = false;

    public int SubscriptionId { get; set; } = 0;

    public List<SmsAttachment>? Attachments { get; set; } = null;

    public bool IsTextMessage { get; set; } = false;

    public bool HasMultipleRecipients { get; set; } = false;
}

public class SmsAttachment
{
    public string? Id { get; set; }
    public string? MimeType { get; set; }
    public string? Base64EncodedFile { get; set; }
}

public class ThreadRequest : SocketMessage
{
    public required long ThreadId { get; set; }

    public long RangeStartTimestamp { get; set; } = -1;

    public long NumberToRequest { get; set; } = -1;
}

public class ContactInfo : SocketMessage
{
    public required string Id { get; set; }

    public required string LookupKey { get; set; }

    public required string DisplayName { get; set; }

    public required string Number { get; set; }

    public string? PhotoBase64 { get; set; }
}

// --- Notifications ---
public class NotificationInfo : SocketMessage
{
    public required string NotificationKey { get; set; }

    public NotificationInfoType InfoType { get; set; }

    public string? TimeStamp { get; set; }

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

    public string? LargeIcon { get; set; }
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

public class FileTransferInfo : SocketMessage
{
    public required List<FileMetadata> Files { get; set; }

    public required ServerInfo ServerInfo { get; set; }

    public bool IsClipboard { get; set; }
}

public class SftpServerInfo : SocketMessage
{
    public required string Username { get; set; }

    public required string Password { get; set; }

    public required string IpAddress { get; set; }

    public int Port { get; set; }
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
}
