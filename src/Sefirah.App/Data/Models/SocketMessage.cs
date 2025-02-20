using Microsoft.UI.Xaml.Media.Imaging;
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
public class SocketMessage { }

public class Misc : SocketMessage
{
    [JsonPropertyName("miscType")]
    public required string MiscType { get; set; }
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
    public required string NotificationType { get; set; }

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
    public string? MediaAction { get; set; }

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
