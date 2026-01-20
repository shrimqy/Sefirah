using Sefirah.Data.Enums;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AuthenticationMessage), typeDiscriminator: "0")]
[JsonDerivedType(typeof(PairMessage), typeDiscriminator: "1")]
[JsonDerivedType(typeof(UdpBroadcast), typeDiscriminator: "2")]
[JsonDerivedType(typeof(DeviceInfo), typeDiscriminator: "3")]
[JsonDerivedType(typeof(BatteryStatus), typeDiscriminator: "4")]
[JsonDerivedType(typeof(RingerMode), typeDiscriminator: "5")]
[JsonDerivedType(typeof(DndStatus), typeDiscriminator: "6")]
[JsonDerivedType(typeof(AudioStreamMessage), typeDiscriminator: "7")]
[JsonDerivedType(typeof(AudioDevice), typeDiscriminator: "8")]
[JsonDerivedType(typeof(CommandMessage), typeDiscriminator: "9")]
[JsonDerivedType(typeof(TextMessage), typeDiscriminator: "10")]
[JsonDerivedType(typeof(TextConversation), typeDiscriminator: "11")]
[JsonDerivedType(typeof(ThreadRequest), typeDiscriminator: "12")]
[JsonDerivedType(typeof(ContactMessage), typeDiscriminator: "13")]
[JsonDerivedType(typeof(NotificationMessage), typeDiscriminator: "14")]
[JsonDerivedType(typeof(NotificationAction), typeDiscriminator: "15")]
[JsonDerivedType(typeof(ReplyAction), typeDiscriminator: "16")]
[JsonDerivedType(typeof(FileTransferMessage), typeDiscriminator: "17")]
[JsonDerivedType(typeof(SftpServerInfo), typeDiscriminator: "18")]
[JsonDerivedType(typeof(ClipboardMessage), typeDiscriminator: "19")]
[JsonDerivedType(typeof(PlaybackSession), typeDiscriminator: "20")]
[JsonDerivedType(typeof(PlaybackAction), typeDiscriminator: "21")]
[JsonDerivedType(typeof(ApplicationList), typeDiscriminator: "22")]
[JsonDerivedType(typeof(ActionMessage), typeDiscriminator: "23")]
public class SocketMessage { }

public class AuthenticationMessage : SocketMessage
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
    public required bool Pair { get; set; }
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

public class BatteryStatus : SocketMessage
{
    public int BatteryLevel { get; set; }

    public bool IsCharging { get; set; }
}

public class RingerMode : SocketMessage
{
    public int Mode { get; set; }
}

public class DndStatus : SocketMessage
{
    public bool IsEnabled { get; set; }
}

public class AudioStreamMessage : SocketMessage
{
    public AudioStreamType StreamType { get; set; }

    public int Level { get; set; }

    public int MaxLevel { get; set; }
}


public class CommandMessage : SocketMessage
{
    public CommandType CommandType { get; set; }
}

public class TextMessage : SocketMessage
{
    public List<string> Addresses { get; set; } = [];

    public long? ThreadId { get; set; } = null;

    public required string Body { get; set; }
        
    public long Timestamp { get; set; }
                    
    public int MessageType { get; set; }
    
    public bool Read { get; set; } = false;

    public long UniqueId { get; set; }

    public int SubscriptionId { get; set; } = 0;

    public List<SmsAttachment>? Attachments { get; set; } = null;

    public bool IsTextMessage { get; set; } = false;

    public bool HasMultipleRecipients { get; set; } = false;
}

public class SmsAttachment
{

    public string? Base64Data { get; set; }

    public string? FileName { get; set; }
}

public class TextConversation : SocketMessage
{
    public required ConversationType ConversationType { get; set; }

    public required long ThreadId { get; set; }

    public List<string> Recipients { get; set; } = [];

    public List<TextMessage> Messages { get; set; } = [];
}

public class ThreadRequest: SocketMessage
{
    public required long ThreadId { get; set; }
    
    public long RangeStartTimestamp { get; set; } = -1;

    public long NumberToRequest { get; set; } = -1;
}

public class ContactMessage : SocketMessage
{
    public required string Id { get; set; }

    public string? LookupKey { get; set; }

    public required string DisplayName { get; set; }

    public required string Number { get; set; }

    public string? PhotoBase64 { get; set; }
}

public class NotificationMessage : SocketMessage
{
    public required string NotificationKey { get; set; }

    public required NotificationType NotificationType { get; set; }

    public string? TimeStamp { get; set; }

    public string? AppPackage { get; set; }

    public string? AppName { get; set; }

    public string? Title { get; set; }

    public string? Text { get; set; }

    public List<NotificationTextMessage> Messages { get; set; } = [];
    public string? GroupKey { get; set; }

    public string? Tag { get; set; }

    public List<NotificationAction> Actions { get; set; } = [];

    public string? ReplyResultKey { get; set; }

    public string? AppIcon { get; set; }

    public string? BigPicture { get; set; }

    public string? LargeIcon { get; set; }
}

public class NotificationTextMessage
{
    public required string Sender { get; set; }

    public required string Text { get; set; }
}

public class NotificationAction : SocketMessage
{
    public required string NotificationKey { get; set; }

    public string? Label { get; set; } = string.Empty;

    public required int ActionIndex { get; set; }

    public bool IsReplyAction { get; set; }
}

public class ReplyAction : SocketMessage
{
    public required string NotificationKey { get; set; }

    public required string ReplyResultKey { get; set; }

    public required string ReplyText { get; set; }
}

public class FileTransferMessage : SocketMessage
{
    public required List<FileMetadata> Files { get; set; }

    public required ServerInfo ServerInfo { get; set; }

    public bool IsClipboard { get; set; }
}

public class ServerInfo
{
    public required int Port { get; set; }

    public required string Password { get; set; }
}

public class FileMetadata
{
    public required string FileName { get; set; }

    public required string MimeType { get; set; }

    public required long FileSize { get; set; }
}

public class SftpServerInfo : SocketMessage
{
    public required string Username { get; set; }

    public required string Password { get; set; }

    public required string IpAddress { get; set; }

    public int Port { get; set; }
}

public class ClipboardMessage : SocketMessage
{
    public string ClipboardType { get; set; } = "text/plain";

    public string Content { get; set; } = string.Empty;
}

public class PlaybackSession : SocketMessage
{
    public SessionType SessionType { get; set; }

    public string? Source { get; set; }

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
}

public class PlaybackAction : SocketMessage
{
    public PlaybackActionType PlaybackActionType { get; set; }

    public required string Source { get; set; }

    public double? Value { get; set; }
}

public class AudioDevice : SocketMessage
{
    public AudioMessageType AudioDeviceType { get; set; }

    public required string DeviceId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public float Volume { get; set; }

    public bool IsMuted { get; set; }

    public bool IsSelected { get; set; }
}

public class ApplicationList : SocketMessage
{
    public required List<ApplicationInfoMessage> AppList { get; set; }
}

public class ApplicationInfoMessage : SocketMessage
{       
    public required string PackageName { get; set; }

    public required string AppName { get; set; }

    public string? AppIcon { get; set; }

}

public class ActionMessage : SocketMessage
{
    public required string ActionId { get; set; }

    public required string ActionName { get; set; }
}