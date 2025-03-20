using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.App.Data.Enums;
using Sefirah.App.Extensions;
using static Vanara.PInvoke.AdvApi32.INSTALLSPEC;

namespace Sefirah.App.Data.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Misc), typeDiscriminator: "0")]
[JsonDerivedType(typeof(DeviceInfo), typeDiscriminator: "1")]
[JsonDerivedType(typeof(DeviceStatus), typeDiscriminator: "2")]
[JsonDerivedType(typeof(ClipboardMessage), typeDiscriminator: "3")]
[JsonDerivedType(typeof(NotificationMessage), typeDiscriminator: "4")]
[JsonDerivedType(typeof(NotificationAction), typeDiscriminator: "5")]
[JsonDerivedType(typeof(ReplyAction), typeDiscriminator: "6")]
[JsonDerivedType(typeof(PlaybackData), typeDiscriminator: "7")]
[JsonDerivedType(typeof(FileTransfer), typeDiscriminator: "8")]
[JsonDerivedType(typeof(BulkFileTransfer), typeDiscriminator: "9")]
[JsonDerivedType(typeof(ApplicationInfo), typeDiscriminator: "10")]
[JsonDerivedType(typeof(SftpServerInfo), typeDiscriminator: "11")]
[JsonDerivedType(typeof(UdpBroadcast), typeDiscriminator: "12")]
[JsonDerivedType(typeof(DeviceRingerMode), typeDiscriminator: "13")]
[JsonDerivedType(typeof(TextMessage), typeDiscriminator: "14")]
[JsonDerivedType(typeof(TextConversation), typeDiscriminator: "15")]
[JsonDerivedType(typeof(ThreadRequest), typeDiscriminator: "16")]
public class SocketMessage { }

public class Misc : SocketMessage
{
    [JsonPropertyName("miscType")]
    public required MiscType MiscType { get; set; }
}

public class ClipboardMessage : SocketMessage
{
    [JsonPropertyName("clipboardType")]
    public string ClipboardType { get; set; } = "text/plain";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class NotificationMessage : SocketMessage
{
    [JsonPropertyName("notificationKey")]
    public required string NotificationKey { get; set; }

    [JsonPropertyName("timestamp")]
    public string? TimeStamp { get; set; }

    [JsonPropertyName("notificationType")]
    public required NotificationType NotificationType { get; set; }

    [JsonPropertyName("appName")]
    public string? AppName { get; set; }

    [JsonPropertyName("appPackage")]
    public string? AppPackage { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("messages")]
    public List<Message>? Messages { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("groupKey")]
    public string? GroupKey { get; set; }

    [JsonPropertyName("actions")]
    public List<NotificationAction?> Actions { get; set; } = [];

    [JsonPropertyName("replyResultKey")]
    public string? ReplyResultKey { get; set; }

    [JsonPropertyName("appIcon")]
    public string? AppIcon { get; set; }

    [JsonPropertyName("bigPicture")]
    public string? BigPicture { get; set; }

    [JsonPropertyName("largeIcon")]
    public string? LargeIcon { get; set; }
}

public class ReplyAction : SocketMessage
{
    [JsonPropertyName("notificationKey")]
    public required string NotificationKey { get; set; }

    [JsonPropertyName("replyResultKey")]
    public required string ReplyResultKey { get; set; }

    [JsonPropertyName("replyText")]
    public required string ReplyText { get; set; }
}

public class NotificationAction : SocketMessage
{
    [JsonPropertyName("notificationKey")]
    public required string NotificationKey { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; } = string.Empty;

    [JsonPropertyName("actionIndex")]
    public required int ActionIndex { get; set; }

    [JsonPropertyName("isReplyAction")]
    public bool IsReplyAction { get; set; }
}

public class GroupedMessage
{
    public string Sender { get; set; }
    public List<string> Messages { get; set; }
}

public class Message
{
    [JsonPropertyName("sender")]
    public string Sender { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public class DeviceInfo : SocketMessage
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; }

    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }

    [JsonPropertyName("proof")]
    public string? Proof { get; set; }

    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; }

    [JsonPropertyName("phoneNumbers")]
    public List<PhoneNumber> PhoneNumbers { get; set; } = [];
}

public class DeviceStatus : SocketMessage
{
    [JsonPropertyName("batteryStatus")]
    public int BatteryStatus { get; set; }

    [JsonPropertyName("chargingStatus")]
    public bool ChargingStatus { get; set; }

    [JsonPropertyName("wifiStatus")]
    public bool WifiStatus { get; set; }

    [JsonPropertyName("bluetoothStatus")]
    public bool BluetoothStatus { get; set; }

    [JsonPropertyName("isDndEnabled")]
    public bool IsDndEnabled { get; set; }

    [JsonPropertyName("ringerMode")]
    public int RingerMode { get; set; }
}

public class PlaybackData : SocketMessage
{
    [JsonPropertyName("appName")]
    public string? AppName { get; set; }

    [JsonPropertyName("trackTitle")]
    public string? TrackTitle { get; set; }

    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    [JsonPropertyName("isPlaying")]
    public bool? IsPlaying { get; set; }

    [JsonPropertyName("position")]
    public long? Position { get; set; }

    [JsonPropertyName("maxSeekTime")]
    public long? MaxSeekTime { get; set; }

    [JsonPropertyName("minSeekTime")]
    public long? MinSeekTime { get; set; }

    [JsonPropertyName("mediaAction")]
    public MediaAction? MediaAction { get; set; }

    [JsonPropertyName("volume")]
    public float Volume { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("appIcon")]
    public string? AppIcon { get; set; }

}

public class FileTransfer : SocketMessage
{
    [JsonPropertyName("fileMetadata")]
    public required FileMetadata FileMetadata { get; set; }

    [JsonPropertyName("serverInfo")]
    public required ServerInfo ServerInfo { get; set; }
}

public class BulkFileTransfer : SocketMessage
{
    [JsonPropertyName("files")]
    public required List<FileMetadata> Files { get; set; }

    [JsonPropertyName("serverInfo")]
    public required ServerInfo ServerInfo { get; set; }
}

public class ServerInfo
{
    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public required int Port { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class FileMetadata
{
    [JsonPropertyName("fileName")]
    public required string FileName { get; set; }

    [JsonPropertyName("mimeType")]
    public required string MimeType { get; set; }

    [JsonPropertyName("fileSize")]
    public required long FileSize { get; set; }

    [JsonIgnore]
    public string? Uri { get; set; } = string.Empty;
}

public class StorageInfo : SocketMessage
{
    [JsonPropertyName("totalSpace")]
    public long TotalSpace { get; set; }

    [JsonPropertyName("freeSpace")]
    public long FreeSpace { get; set; }

    [JsonPropertyName("usedSpace")]
    public long UsedSpace { get; set; }

}

public class ScreenData : SocketMessage
{
    [JsonPropertyName("timestamp")]
    public long TimeStamp { get; set; }

}

public class ApplicationInfo : SocketMessage
{
    [JsonPropertyName("packageName")]
    public required string PackageName { get; set; }

    [JsonPropertyName("appName")]
    public required string AppName { get; set; }

    [JsonPropertyName("appIcon")]
    public string? AppIcon { get; set; }

}

public class SftpServerInfo : SocketMessage
{
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("password")]
    public required string Password { get; set; }

    [JsonPropertyName("ipAddress")]
    public required string IpAddress { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; }
}

public class UdpBroadcast : SocketMessage
{
    [JsonPropertyName("ipAddresses")]
    public List<string> IpAddresses { get; set; } = [];

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; set; }

    [JsonPropertyName("deviceName")]
    public required string DeviceName { get; set; }

    [JsonPropertyName("publicKey")]
    public required string PublicKey { get; set; }

    [JsonPropertyName("timestamp")]
    public long TimeStamp { get; set; }
}

public class DeviceRingerMode : SocketMessage
{
    [JsonPropertyName("ringerMode")]
    public int RingerMode { get; set; }
}

public class TextConversation : SocketMessage
{
    [JsonPropertyName("conversationType")]
    public required ConversationType ConversationType { get; set; }

    [JsonPropertyName("threadId")]
    public required long ThreadId { get; set; }

    [JsonPropertyName("messages")]
    public List<TextMessage> Messages { get; set; } = [];
}

public class TextMessage : SocketMessage
{
    [JsonPropertyName("addresses")]
    public List<SmsAddress> Addresses { get; set; } = [];

    [JsonPropertyName("contacts")]
    public List<Contact> Contacts { get; set; } = [];

    [JsonPropertyName("threadId")]
    public long? ThreadId { get; set; } = null;

    [JsonPropertyName("body")]
    public required string Body { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("messageType")]
    public int MessageType { get; set; }
    
    [JsonPropertyName("read")]
    public bool Read { get; set; } = false;

    [JsonPropertyName("uniqueId")]
    public long UniqueId { get; set; }

    [JsonPropertyName("subscriptionId")]
    public int SubscriptionId { get; set; } = 0;

    [JsonPropertyName("attachments")]
    public List<SmsAttachment>? Attachments { get; set; } = null;

    [JsonPropertyName("isTextMessage")]
    public bool IsTextMessage { get; set; } = false;

    [JsonPropertyName("hasMultipleRecipients")]
    public bool HasMultipleRecipients { get; set; } = false;
}

public class SmsAddress
{
    [JsonPropertyName("address")]
    public required string Address { get; set; }
}

public class SmsAttachment
{
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; set; }

    [JsonPropertyName("base64EncodedFile")]
    public string? Base64EncodedFile { get; set; }

    [JsonPropertyName("fileName")]
    public required string FileName { get; set; }
}

public class ThreadRequest: SocketMessage
{
    [JsonPropertyName("threadId")]
    public required long ThreadId { get; set; }
    
    [JsonPropertyName("rangeStartTimestamp")]
    public long RangeStartTimestamp { get; set; } = -1;

    [JsonPropertyName("numberToRequest")]
    public long NumberToRequest { get; set; } = -1;
}

public class PhoneNumber
{
    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("subscriptionId")]
    public int SubscriptionId { get; set; } = -1;
}

public class Contact
{
    [JsonPropertyName("contactName")]
    public required string ContactName { get; set; }

    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; set; }

    [JsonPropertyName("photoBase64")]
    public string? PhotoBase64 { get; set; }
}
