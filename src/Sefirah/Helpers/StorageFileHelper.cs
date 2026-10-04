using Microsoft.UI.Xaml.Media.Imaging;

namespace Sefirah.Helpers;

public static class StorageFileHelper
{
    public static async Task<BitmapImage?> ToBitmapAsync(IStorageFile file, int decodePixelWidth = 600)
    {
        try
        {
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var bitmap = new BitmapImage();
            if (decodePixelWidth > 0)
                bitmap.DecodePixelWidth = decodePixelWidth;
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
