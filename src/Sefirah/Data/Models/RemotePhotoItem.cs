using Microsoft.UI.Xaml.Media.Imaging;

namespace Sefirah.Data.Models;

/// <summary>
/// An image on the device, shown in the gallery. The preview and the local copy fill in as they
/// are fetched, which is why they are observable rather than set once.
/// </summary>
public sealed partial class RemotePhotoItem : ObservableObject
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public long Size { get; init; }

    public DateTime LastModified { get; init; }

    [ObservableProperty]
    public partial BitmapImage? Preview { get; set; }

    /// <summary>
    /// Set once the full image has been pulled down and cached, so later actions skip the transfer.
    /// </summary>
    public string? LocalPath { get; set; }

    public string Tooltip => $"{Name}{Environment.NewLine}{LastModified:g}   {FormatSize(Size)}";

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} {units[0]}" : $"{size:0.#} {units[unit]}";
    }
}
