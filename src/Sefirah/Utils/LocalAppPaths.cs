namespace Sefirah.Utils;

public static class LocalAppPaths
{
    public const string DevicesFolderName = "Devices";
    public const string DeviceSettingsFileName = "settings.json";
    public const string DeviceIconsFolderName = "Icons";
    public const string UserSettingsFileName = "user_settings.json";

    private static string LocalFolder => ApplicationData.Current.LocalFolder.Path;

    public static string GetUserSettingsPath() =>
        Path.Combine(LocalFolder, UserSettingsFileName);

    public static string GetDeviceFolder(string deviceId) =>
        Path.Combine(LocalFolder, DevicesFolderName, deviceId);

    public static string GetDeviceSettingsPath(string deviceId) =>
        Path.Combine(GetDeviceFolder(deviceId), DeviceSettingsFileName);

    public static string GetDeviceIconsFolder(string deviceId) =>
        Path.Combine(GetDeviceFolder(deviceId), DeviceIconsFolderName);

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
