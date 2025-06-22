using Windows.Storage;
using Windows.Storage.Streams;

namespace Sefirah.Utils;

/// <summary>
/// Utility class for image operations
/// </summary>
public static class ImageUtils
{
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

        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            using var dataWriter = new DataWriter(stream);
            dataWriter.WriteBytes(bytes);
            await dataWriter.StoreAsync();
        }

        return new Uri($"ms-appdata:///local/{fileName}");
    }

    /// <summary>
    /// Gets the URI for a file in the local app data folder
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <returns>URI to the file</returns>
    public static Uri GetLocalFileUri(string fileName)
    {
        return new Uri($"ms-appdata:///local/{fileName}");
    }

    /// <summary>
    /// Checks if a file exists in the local app data folder
    /// </summary>
    /// <param name="fileName">Name of the file to check</param>
    /// <returns>True if the file exists, false otherwise</returns>
    public static async Task<bool> LocalFileExistsAsync(string fileName)
    {
        try
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            await localFolder.GetFileAsync(fileName);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    public static async Task<Uri> SaveToFileAsync(byte[]? bytes, string fileName)
    {
        if (bytes == null) return null;
        
        var localFolder = ApplicationData.Current.LocalFolder;
        var file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            using var dataWriter = new DataWriter(stream);
            dataWriter.WriteBytes(bytes);
            await dataWriter.StoreAsync();
        }
        return new Uri($"ms-appdata:///local/{fileName}");
    }

    /// <summary>
    /// Saves bytes to a file and returns the file system path (needed for native applications like scrcpy)
    /// </summary>
    /// <param name="bytes">Bytes to save</param>
    /// <param name="fileName">Name of the file to save</param>
    /// <returns>File system path to the saved file</returns>
    public static async Task<string?> SaveToFilePathAsync(byte[]? bytes, string fileName)
    {
        if (bytes == null) return null;
        
        var localFolder = ApplicationData.Current.LocalFolder;
        var file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            using var dataWriter = new DataWriter(stream);
            dataWriter.WriteBytes(bytes);
            await dataWriter.StoreAsync();
        }
        return file.Path;
    }
} 
