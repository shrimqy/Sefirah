using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Sefirah.Data.Models;

/// <summary>
/// A past clipboard item from the device, as shown in the history.
/// </summary>
public sealed class ClipboardEntry
{
    public int Id { get; init; }

    public required string ClipboardType { get; init; }

    /// <summary>The text itself, or the path of the stored image.</summary>
    public required string Content { get; init; }

    public bool IsImage { get; init; }

    public DateTime Timestamp { get; init; }

    public bool IsText => !IsImage;

    private ImageSource? thumbnail;

    /// <summary>
    /// Exposed as an ImageSource rather than a path: binding a string leaves WinUI to convert it,
    /// and it refuses both an empty string and a null, taking the app down with it.
    /// </summary>
    public ImageSource? Thumbnail
    {
        get
        {
            if (thumbnail is not null) return thumbnail;
            if (!IsImage || !File.Exists(Content)) return null;

            var bitmap = new BitmapImage { DecodePixelWidth = 128 };
            bitmap.UriSource = new Uri(Content);
            thumbnail = bitmap;
            return thumbnail;
        }
    }

    /// <summary>Kept to a couple of lines: the list is for recognising an item, not reading it.</summary>
    public string Preview => IsImage
        ? Path.GetFileName(Content)
        : Content.Length > 220 ? string.Concat(Content.AsSpan(0, 220), "…") : Content;

    public string TimestampText => Timestamp.ToString("g");
}
