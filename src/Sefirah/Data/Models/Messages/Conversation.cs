using Microsoft.UI.Xaml.Media.Imaging;

namespace Sefirah.Data.Models.Messages;

public partial class Conversation : ObservableObject
{
    public long ThreadId { get; set; }
    public List<Contact> Contacts { get; set; } = [];
    public string? AvatarGlyph { get; set; } = string.Empty;
    public BitmapImage? AvatarImage { get; set; }
    public string DisplayName => Contacts.Count != 0 ? string.Join(", ", Contacts.Select(s => !string.IsNullOrEmpty(s.DisplayName) ? s.DisplayName : s.Address)) : "Unknown";

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
        AvatarGlyph = other.AvatarGlyph;
        AvatarImage = other.AvatarImage;
        OnPropertyChanged(nameof(DisplayName));
    }
}
