using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Sefirah.Helpers;

public sealed record ActionIconData(string Code, string Name, IReadOnlyList<string> Tags);

/// <summary>
/// Catalog icons by Name, or a persisted custom icon file path in Icon.
/// </summary>
public static class ActionIconHelper
{
    private static readonly Lazy<Dictionary<string, ActionIconData>> byName = new(LoadCatalog);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".ico", ".gif", ".bmp"
    };

    public static IEnumerable<ActionIconData> Catalog => byName.Value.Values;

    public static bool IsKnown(string? icon) => Resolve(icon) is not null;

    /// <summary>Absolute file path for a user-chosen custom icon.</summary>
    public static bool IsIconPath(string? icon) =>
        !string.IsNullOrWhiteSpace(icon) && !IsKnown(icon) && Path.IsPathRooted(icon);

    public static string GlyphForIcon(string? icon)
    {
        if (Resolve(icon) is { } data &&
            ushort.TryParse(data.Code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
        {
            return char.ConvertFromUtf32(code);
        }

        return "\uE71D";
    }

    public static ImageSource? ImageSourceForIcon(string? icon)
    {
        if (!IsIconPath(icon))
        {
            return null;
        }

        try
        {
            return new BitmapImage(new Uri(icon!));
        }
        catch
        {
            return null;
        }
    }

    public static Visibility CustomIconVisibility(string? icon) =>
        IsIconPath(icon) ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility BuiltInIconVisibility(string? icon) =>
        IsIconPath(icon) ? Visibility.Collapsed : Visibility.Visible;

    public static ActionIconData? Resolve(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        return byName.Value.TryGetValue(icon, out var exact) ? exact : null;
    }

    private static bool IsImagePath(string filePath) =>
        ImageExtensions.Contains(Path.GetExtension(filePath));

    /// <summary>
    /// Icon bytes for a path: image files are read directly; otherwise Windows thumbnail API as fallback.
    /// Wire/base64 for sending to devices.
    /// </summary>
    public static async Task<string?> EncodeFromPathAsync(string filePath)
    {
        var bytes = await ReadIconBytesFromPathAsync(filePath);
        return bytes is { Length: > 0 } ? Convert.ToBase64String(bytes) : null;
    }

    public static async Task<byte[]?> ReadIconBytesFromPathAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        if (IsImagePath(filePath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath);
                return bytes.Length > 0 ? bytes : null;
            }
            catch
            {
                return null;
            }
        }

        return await ReadThumbnailBytesFromPathAsync(filePath);
    }

#if WINDOWS
    private static Task<byte[]?> ReadThumbnailBytesFromPathAsync(string filePath) =>
        App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                using var thumb = await file.GetThumbnailAsync(
                    Windows.Storage.FileProperties.ThumbnailMode.SingleItem,
                    64,
                    Windows.Storage.FileProperties.ThumbnailOptions.ResizeThumbnail);
                if (thumb is null || thumb.Size == 0)
                {
                    return null;
                }

                thumb.Seek(0);
                var bytes = new byte[thumb.Size];
                await thumb.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
                return bytes.Length > 0 ? bytes : null;
            }
            catch
            {
                return null;
            }
        });
#else
    private static Task<byte[]?> ReadThumbnailBytesFromPathAsync(string filePath) =>
        Task.FromResult<byte[]?>(null);
#endif

    private static Dictionary<string, ActionIconData> LoadCatalog()
    {
        try
        {
            foreach (var path in CandidatePaths())
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var items = JsonSerializer.Deserialize<List<ActionIconDataDto>>(File.ReadAllText(path));
                if (items is { Count: > 0 })
                {
                    return items.ToDictionary(
                        i => i.Name,
                        i => new ActionIconData(i.Code, i.Name, i.Tags ?? []),
                        StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
        }

        return new(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> CandidatePaths()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "Assets", "ActionIconsData.json");
        yield return Path.Combine(baseDir, "ActionIconsData.json");
    }

    private sealed class ActionIconDataDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
    }
}
