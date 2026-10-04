namespace Sefirah.Data.Models.Messages;

public abstract class AttachmentBase : ObservableObject
{
    public string FileName { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public AttachmentKind Kind => FromMime(MimeType);

    public bool IsImage => Kind is AttachmentKind.Image;

    public bool IsVideo => Kind is AttachmentKind.Video;

    public bool IsAudio => Kind is AttachmentKind.Audio;

    public abstract bool HasPreview { get; }

    public abstract ImageSource? PreviewImage { get; }

    public bool ShowPlaceholder => !HasPreview;

    public string DisplayTitle => string.IsNullOrWhiteSpace(FileName) ? MimeType : Path.GetFileName(FileName);

    public static AttachmentKind FromMime(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return AttachmentKind.Other;

        if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return AttachmentKind.Image;
        if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return AttachmentKind.Video;
        if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return AttachmentKind.Audio;

        return AttachmentKind.Other;
    }

    public static string TypeLabel(string? mimeType) => TypeLabel(FromMime(mimeType));

    public static string TypeLabel(AttachmentKind kind) => kind switch
    {
        AttachmentKind.Image => "MessagePhoto".GetLocalizedResource(),
        AttachmentKind.Video => "MessageVideo".GetLocalizedResource(),
        AttachmentKind.Audio => "MessageAudio".GetLocalizedResource(),
        _ => "MessageAttachment".GetLocalizedResource()
    };
}
