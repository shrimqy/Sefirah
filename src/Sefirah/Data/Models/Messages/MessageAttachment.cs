using Microsoft.UI.Xaml.Media.Imaging;

namespace Sefirah.Data.Models.Messages;

public partial class MessageAttachment : AttachmentBase
{
    public long PartId { get; set; } = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFullFile), nameof(ImagePath), nameof(HasPreview), nameof(ShowPlaceholder), nameof(ShowPlayOverlay), nameof(PreviewImage))]
    public partial string? FilePath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImagePath), nameof(HasPreview), nameof(ShowPlaceholder), nameof(ShowPlayOverlay), nameof(PreviewImage))]
    public partial string? PreviewPath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayOverlay))]
    public partial bool IsLoading { get; set; }

    public bool HasFullFile => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    public bool IsGif => MimeType.Equals("image/gif", StringComparison.OrdinalIgnoreCase);

    public override bool HasPreview => !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath);

    public string? ImagePath =>
        IsImage && HasFullFile
            ? FilePath
            : PreviewPath ?? (IsImage ? FilePath : null);

    public override ImageSource? PreviewImage => previewImage ??= LoadPreviewImage();

    public bool ShowPlayOverlay => IsVideo && HasPreview && !IsLoading;

    public string PlaceholderGlyph => Kind switch
    {
        AttachmentKind.Video => "\uE714",
        AttachmentKind.Audio => "\uE8D6",
        AttachmentKind.Image => "\uE91B",
        _ => "\uE8A5"
    };

    public string PlaceholderLabel => TypeLabel(Kind);

    public bool ShowTypeCaption => !string.IsNullOrWhiteSpace(FileName);

    private ImageSource? previewImage;

    partial void OnFilePathChanged(string? value) => previewImage = null;

    partial void OnPreviewPathChanged(string? value) => previewImage = null;

    private BitmapImage? LoadPreviewImage()
    {
        var path = ImagePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage
            {
                DecodePixelWidth = 600,
                UriSource = new Uri(path)
            };
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
