using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Sefirah.Helpers;

public static class ImageHelper
{
    public static async Task<BitmapImage?> ToBitmapAsync(this byte[]? data, int decodeSize = -1)
    {
        if (data is null) return null;
        try
        {
            using var ms = new MemoryStream(data);
            var image = new BitmapImage();
            if (decodeSize > 0)
            {
                image.DecodePixelWidth = decodeSize;
                image.DecodePixelHeight = decodeSize;
            }
            image.DecodePixelType = DecodePixelType.Logical;
            await image.SetSourceAsync(ms.AsRandomAccessStream());
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<string> ToBase64Async(this IRandomAccessStreamReference data)
    {
        try
        {
            using var stream = await data.OpenReadAsync();
            var reader = new DataReader(stream.GetInputStreamAt(0));
            var bytes = new byte[stream.Size];
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
