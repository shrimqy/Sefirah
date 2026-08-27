namespace Sefirah.Utils;

public static class LocalAppPaths
{
    public const string DevicesFolderName = "Devices";
    public const string DeviceSettingsFileName = "settings.json";
    public const string DeviceIconsFolderName = "Icons";
    public const string ClipboardFolderName = "Clipboard";
    public const string ClipboardHistoryFolderName = "ClipboardHistory";
    public const string UserSettingsFileName = "user_settings.json";

    private static readonly TimeSpan TemporaryFileMaxAge = TimeSpan.FromHours(24);

    private static string LocalFolder => ApplicationData.Current.LocalFolder.Path;
    private static string TemporaryFolder => ApplicationData.Current.TemporaryFolder.Path;

    public static string GetUserSettingsPath() =>
        Path.Combine(LocalFolder, UserSettingsFileName);

    public static string GetDeviceFolder(string deviceId) =>
        Path.Combine(LocalFolder, DevicesFolderName, deviceId);

    public static string GetDeviceSettingsPath(string deviceId) =>
        Path.Combine(GetDeviceFolder(deviceId), DeviceSettingsFileName);

    public static string GetDeviceIconsFolder(string deviceId) =>
        Path.Combine(GetDeviceFolder(deviceId), DeviceIconsFolderName);

    public static string GetClipboardFolder()
    {
        var path = Path.Combine(TemporaryFolder, ClipboardFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string CreateClipboardFilePath(string? extension)
    {
        var ext = string.IsNullOrWhiteSpace(extension)
            ? ".png"
            : extension[0] == '.' ? extension : $".{extension}";
        return Path.Combine(GetClipboardFolder(), $"clipboard_{Guid.NewGuid():N}{ext}");
    }

    /// <summary>
    /// Clipboard images live in the temporary folder, which is pruned daily. History entries need to
    /// outlive that, so their images are kept here instead.
    /// </summary>
    public static string GetClipboardHistoryFolder()
    {
        var path = Path.Combine(LocalFolder, ClipboardHistoryFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string CreateClipboardHistoryFilePath(string? extension)
    {
        var ext = string.IsNullOrWhiteSpace(extension)
            ? ".png"
            : extension[0] == '.' ? extension : $".{extension}";
        return Path.Combine(GetClipboardHistoryFolder(), $"clipboard_{Guid.NewGuid():N}{ext}");
    }

    public static void PruneTemporaryFolder()
    {
        try
        {
            var root = TemporaryFolder;
            if (!Directory.Exists(root)) return;

            var cutoff = DateTime.UtcNow - TemporaryFileMaxAge;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    public static string GetAppIconFilePath(string deviceId, string packageName) =>
        Path.Combine(GetDeviceIconsFolder(deviceId), $"{packageName}.png");

    public static string GetAppIconPath(string deviceId, string packageName) =>
        $"ms-appdata:///local/{DevicesFolderName}/{deviceId}/{DeviceIconsFolderName}/{packageName}.png";

    public static void EnsureDeviceFolder(string deviceId) =>
        Directory.CreateDirectory(GetDeviceFolder(deviceId));

    public static void DeleteDeviceIcons(string deviceId)
    {
        try
        {
            var iconsFolder = GetDeviceIconsFolder(deviceId);
            if (Directory.Exists(iconsFolder))
                Directory.Delete(iconsFolder, true);
        }
        catch (Exception)
        {
        }
    }

    public static void DeleteDeviceData(string deviceId)
    {
        try
        {
            var deviceFolder = GetDeviceFolder(deviceId);
            if (Directory.Exists(deviceFolder))
                Directory.Delete(deviceFolder, true);
        }
        catch (Exception)
        {
        }
    }
}
