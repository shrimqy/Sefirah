using Windows.Storage.Streams;

namespace Sefirah.Utils;

/// <summary>
/// Utility class for image operations
/// </summary>
public static class IconUtils
{
    private const string AppIconsFolderName = "AppIcons";

    /// <summary>
    /// Gets or creates the AppIcons folder in the local app data
    /// </summary>
    /// <returns>The AppIcons folder</returns>
    public static async Task<StorageFolder> GetAppIconsFolderAsync()
    {
        var localFolder = ApplicationData.Current.LocalFolder;
        try
        {
            return await localFolder.GetFolderAsync(AppIconsFolderName);
        }
        catch (FileNotFoundException)
        {
            return await localFolder.CreateFolderAsync(AppIconsFolderName);
        }
    }

    /// <summary>
    /// Saves a base64 encoded image to a file and returns the URI
    /// </summary>
    /// <param name="base64">Base64 encoded image data</param>
    /// <param name="fileName">Name of the file to save</param>
    /// <returns>URI to the saved file</returns>
    public static async Task<Uri> SaveBase64ToFileAsync(string base64, string fileName)
    {
        var bytes = Convert.FromBase64String(base64);
        var localFolder = ApplicationData.Current.LocalFolder;
        var file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        using var dataWriter = new DataWriter(stream);
        dataWriter.WriteBytes(bytes);
        await dataWriter.StoreAsync();

        return new Uri($"ms-appdata:///local/{fileName}");
    }


    /// <summary>
    /// Gets the URI for an app icon file in the AppIcons folder
    /// </summary>
    /// <param name="packageName">Name of the app icon file</param>
    /// <returns>URI to the app icon file</returns>
    public static async Task<Uri?> GetAppIconUriAsync(string packageName)
    {
        try
        {
            var appIconsFolder = await GetAppIconsFolderAsync();
            await appIconsFolder.GetFileAsync(packageName);
            return new Uri($"ms-appdata:///local/{AppIconsFolderName}/{packageName}");
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public static string GetAppIconFilePath(string packageName)
    {
        return $"{ApplicationData.Current.LocalFolder.Path}\\{AppIconsFolderName}\\{packageName}.png";
    }

    public static string GetAppIconPath(string packageName)
    {
        return $"ms-appdata:///local/{AppIconsFolderName}/{packageName}.png";
    }

    /// <summary>
    /// Saves app icon bytes to the AppIcons folder and returns the file system path
    /// </summary>
    /// <param name="bytes">App icon bytes to save</param>
    /// <param name="fileName">Name of the app icon file</param>
    /// <returns>File system path to the saved app icon file</returns>
    public static async void SaveAppIconToPathAsync(string? appIconBase64, string appPackage)
    {
        try
        {
            if (string.IsNullOrEmpty(appIconBase64)) return;
            
            var bytes = Convert.FromBase64String(appIconBase64);
            var appIconsFolder = await GetAppIconsFolderAsync();
            var file = await appIconsFolder.CreateFileAsync($"{appPackage}.png", CreationCollisionOption.ReplaceExisting);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            using var dataWriter = new DataWriter(stream);
            dataWriter.WriteBytes(bytes);
            await dataWriter.StoreAsync();
        }
        catch (Exception)
        {
        }
    }
} 
