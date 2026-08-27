namespace Sefirah.Data.Models;

/// <summary>
/// An entry in the device filesystem, as listed over SFTP.
/// </summary>
public sealed class RemoteFileItem
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public bool IsDirectory { get; init; }

    public bool IsFile => !IsDirectory;

    public long Size { get; init; }

    public DateTime LastModified { get; init; }

    /// <summary>
    /// Segoe Fluent Icons code point, the same icon family the rest of the app draws from.
    /// </summary>
    public string Glyph => ((char)(IsDirectory ? 0xE8B7 : GlyphFor(Path.GetExtension(Name)))).ToString();

    public string SizeText => IsDirectory ? string.Empty : FormatSize(Size);

    public string LastModifiedText => LastModified == default ? string.Empty : LastModified.ToString("g");

    private static int GlyphFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".heic" => 0xEB9F,
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".3gp" => 0xE714,
        ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" or ".opus" => 0xE189,
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => 0xF12B,
        ".apk" => 0xECAA,
        ".pdf" or ".txt" or ".md" or ".log" or ".json" or ".xml" or ".doc" or ".docx" => 0xE8A5,
        _ => 0xE7C3
    };

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
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
