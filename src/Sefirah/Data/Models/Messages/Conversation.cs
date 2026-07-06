using System.Globalization;
using Sefirah.Utils;
using Windows.Storage.Streams;

namespace Sefirah.Data.Models.Messages;

public partial class Conversation : ObservableObject
{
    public string ConversationKey { get; set; } = string.Empty;

    public long ThreadId { get; set; }

    public List<Contact> Contacts { get; set; } = [];

    public bool IsGroup => Contacts.Count > 1;

    public Contact? PrimaryContact => Contacts.Count == 1 ? Contacts[0] : null;

    public string DisplayName => Contacts.Count != 0
        ? string.Join(", ", Contacts.Select(c => c.DisplayName))
        : "Unknown";

    public string SubtitleAddress => PrimaryContact?.Address ?? string.Empty;

    public IRandomAccessStreamReference? AvatarStream => PrimaryContact?.AvatarStream;

    public bool HasAvatarImage => PrimaryContact?.HasAvatar ?? false;

    public string PlaceholderColorHex => IsGroup
        ? ContactHelper.GetPlaceholderColorHex(ThreadId.ToString(CultureInfo.InvariantCulture))
        : PrimaryContact?.PlaceholderColorHex ?? ContactHelper.GetPlaceholderColorHex(ThreadId.ToString(CultureInfo.InvariantCulture));

    public string Initials => IsGroup ? string.Empty : PrimaryContact?.Initials ?? string.Empty;

    public bool ShowGroupGlyph => IsGroup;

    public bool ShowContactInitials => !IsGroup && !string.IsNullOrEmpty(Initials);

    public bool ShowContactGlyph => !IsGroup && string.IsNullOrEmpty(Initials);

    private string lastMessage = string.Empty;
    public string LastMessage
    {
        get => lastMessage;
        set => SetProperty(ref lastMessage, value);
    }

    private long lastMessageTimestamp;
    public long LastMessageTimestamp
    {
        get => lastMessageTimestamp;
        set => SetProperty(ref lastMessageTimestamp, value);
    }

    private bool hasRead;
    public bool HasRead
    {
        get => hasRead;
        set => SetProperty(ref hasRead, value);
    }

    public void UpdateFrom(Conversation other)
    {
        LastMessage = other.LastMessage;
        LastMessageTimestamp = other.LastMessageTimestamp;
        HasRead = other.HasRead;
        Contacts = other.Contacts;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(SubtitleAddress));
        OnPropertyChanged(nameof(AvatarStream));
        OnPropertyChanged(nameof(HasAvatarImage));
        OnPropertyChanged(nameof(IsGroup));
        OnPropertyChanged(nameof(PlaceholderColorHex));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(ShowGroupGlyph));
        OnPropertyChanged(nameof(ShowContactInitials));
        OnPropertyChanged(nameof(ShowContactGlyph));
    }
}
