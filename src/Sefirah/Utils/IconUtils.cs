namespace Sefirah.Utils;

/// <summary>
/// Utility class for image operations
/// </summary>
public static class IconUtils
{
    private const string ScrcpyIconsFolderName = "scrcpy-icons";
    private const string ScrcpyWindowIconFileName = "scrcpy.png";

    public static string ScrcpyIconsDirectory =>
        Path.Combine(ApplicationData.Current.LocalFolder.Path, ScrcpyIconsFolderName);

    public static string ScrcpyWindowIconPath =>
        Path.Combine(ScrcpyIconsDirectory, ScrcpyWindowIconFileName);

    public static void SetScrcpyWindowIcon(string deviceId, string packageName)
    {
        try
        {
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(packageName))
                return;

            var appIconPath = LocalAppPaths.GetAppIconFilePath(deviceId, packageName);
            if (!File.Exists(appIconPath))
                return;

            Directory.CreateDirectory(ScrcpyIconsDirectory);
            File.Copy(appIconPath, ScrcpyWindowIconPath, true);
        }
        catch (Exception) { }
    }

    public static Uri? GetAppIconUri(string deviceId, string packageName)
    {
        if (!File.Exists(LocalAppPaths.GetAppIconFilePath(deviceId, packageName)))
            return null;

        return new Uri(LocalAppPaths.GetAppIconPath(deviceId, packageName));
    }

    public static async Task SaveAppIconToPathAsync(string? appIconBase64, string deviceId, string appPackage)
    {
        try
        {
            if (string.IsNullOrEmpty(appIconBase64))
                return;

            var bytes = Convert.FromBase64String(appIconBase64);
            var filePath = LocalAppPaths.GetAppIconFilePath(deviceId, appPackage);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllBytesAsync(filePath, bytes);
        }
        catch (Exception)
        {
        }
    }

    public static void DeleteAppIcon(string deviceId, string appPackage)
    {
        try
        {
            var filePath = LocalAppPaths.GetAppIconFilePath(deviceId, appPackage);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception)
        {
        }
    }
}
