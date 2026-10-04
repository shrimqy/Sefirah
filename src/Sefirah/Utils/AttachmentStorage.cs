namespace Sefirah.Utils;

public static class AttachmentStorage
{
    public static string GetPath(string deviceId, long uniqueId, long partId, string fileName, string mimeType) =>
        Path.Combine(LocalAppPaths.GetAttachmentsFolder(deviceId), $"{uniqueId}_{partId}_{WithExtension(fileName, mimeType)}");

    public static string GetThumbnailPath(string deviceId, long uniqueId, long partId, string fileName) =>
        Path.Combine(LocalAppPaths.GetAttachmentsFolder(deviceId), $"{uniqueId}_{partId}_{Path.GetFileName(fileName)}.thumb.jpg");

    public static string? GetPathIfExists(string deviceId, long uniqueId, long partId, string fileName, string mimeType)
    {
        var path = GetPath(deviceId, uniqueId, partId, fileName, mimeType);
        return File.Exists(path) ? path : null;
    }

    public static string? GetThumbnailPathIfExists(string deviceId, long uniqueId, long partId, string fileName)
    {
        var path = GetThumbnailPath(deviceId, uniqueId, partId, fileName);
        return File.Exists(path) ? path : null;
    }

    public static void SaveThumbnail(string deviceId, long uniqueId, long partId, string fileName, string? base64)
    {
        Write(GetThumbnailPath(deviceId, uniqueId, partId, fileName), base64);
    }

    public static void Save(string deviceId, long uniqueId, long partId, string fileName, string mimeType, string? base64)
    {
        Write(GetPath(deviceId, uniqueId, partId, fileName, mimeType), base64);
    }

    public static void CopyTo(string sourcePath, string deviceId, long uniqueId, long partId, string fileName, string mimeType)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return;

        var dest = GetPath(deviceId, uniqueId, partId, fileName, mimeType);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(sourcePath, dest, overwrite: true);
    }

    public static async Task CopyFromFileAsync(
        IStorageFile file,
        string deviceId,
        long uniqueId,
        long partId,
        string fileName,
        string mimeType)
    {
        var dest = GetPath(deviceId, uniqueId, partId, fileName, mimeType);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        await using var source = await file.OpenStreamForReadAsync();
        await using var target = File.Create(dest);
        await source.CopyToAsync(target);
    }

    /// <summary>Rename stored files when an optimistic UniqueId becomes a Telephony _id.</summary>
    public static void Move(
        string deviceId,
        long fromUniqueId,
        long toUniqueId,
        long partId,
        string fileName,
        string mimeType)
    {
        MoveFile(GetPath(deviceId, fromUniqueId, partId, fileName, mimeType), GetPath(deviceId, toUniqueId, partId, fileName, mimeType));
        MoveFile(
            GetThumbnailPath(deviceId, fromUniqueId, partId, fileName),
            GetThumbnailPath(deviceId, toUniqueId, partId, fileName));
    }

    private static void MoveFile(string source, string dest)
    {
        if (!File.Exists(source))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (File.Exists(dest))
            File.Delete(dest);
        File.Move(source, dest);
    }

    public static void Delete(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        File.Delete(filePath);
    }

    public static void DeleteAll(string deviceId, long uniqueId, long partId, string fileName, string mimeType)
    {
        Delete(GetPath(deviceId, uniqueId, partId, fileName, mimeType));
        Delete(GetThumbnailPath(deviceId, uniqueId, partId, fileName));
    }

    public static void DeleteAllForDevice(string deviceId)
    {
        var folder = LocalAppPaths.GetAttachmentsFolder(deviceId);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
    }

    private static void Write(string path, string? base64)
    {
        if (string.IsNullOrEmpty(base64))
            return;

        byte[] data;
        try
        {
            data = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return;
        }

        if (data.Length == 0)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    private static string WithExtension(string fileName, string mimeType)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
            name = "attachment";

        return Path.HasExtension(name) ? name : name + ExtensionFromMime(mimeType);
    }

    private static string ExtensionFromMime(string mimeType) => mimeType.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "image/bmp" => ".bmp",
        "image/heic" => ".heic",
        "video/mp4" => ".mp4",
        "video/3gpp" => ".3gp",
        "audio/mpeg" => ".mp3",
        "audio/mp4" => ".m4a",
        "audio/aac" => ".aac",
        "audio/ogg" => ".ogg",
        "audio/amr" => ".amr",
        _ => string.Empty
    };
}
