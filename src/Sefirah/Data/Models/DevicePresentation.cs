namespace Sefirah.Data.Models;

/// <summary>Source-owned display preferences. No hardware identity is inferred from a name.</summary>
public sealed class DevicePresentation
{
    public int Version { get; set; } = 1;
    public string Kind { get; set; } = "unknown";
    public string Orientation { get; set; } = "landscape";
    public string AspectRatio { get; set; } = "16:9";
    // none explicitly revokes the image; null DeviceInfo.presentation is no update.
    public string WallpaperSource { get; set; } = "none";
    public string? WallpaperId { get; set; }
    public string? Wallpaper { get; set; }

    public static readonly string[] Kinds = ["unknown", "phone", "tablet", "computer"];
    public static readonly string[] Ratios = ["9:16", "9:19.5", "9:20", "4:3", "3:2", "16:9", "16:10"];
    public bool IsValid => Version == 1 && Kinds.Contains(Kind)
        && Orientation is "portrait" or "landscape" && Ratios.Contains(AspectRatio)
        && WallpaperSource is "none" or "wallpaper" or "userSelected"
        && (WallpaperSource == "none" || WallpaperId is { Length: 64 } && WallpaperId.All(Uri.IsHexDigit));
    [JsonIgnore]
    public double DisplayRatio
    {
        get
        {
            if (!IsValid) return 1;
            var parts = AspectRatio.Split(':');
            var ratio = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture)
                / double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            ratio = Math.Max(ratio, 1 / ratio);
            return Orientation == "portrait" ? 1 / ratio : ratio;
        }
    }
    public DevicePresentation Metadata() => new()
    {
        Version = Version, Kind = Kind, Orientation = Orientation, AspectRatio = AspectRatio,
        WallpaperSource = WallpaperSource, WallpaperId = WallpaperId
    };
    public static DevicePresentation? Parse(string? value)
    {
        try
        {
            var result = string.IsNullOrEmpty(value) ? null : JsonSerializer.Deserialize<DevicePresentation>(value);
            return result?.IsValid == true ? result : null;
        }
        catch (JsonException) { return null; }
    }
}
