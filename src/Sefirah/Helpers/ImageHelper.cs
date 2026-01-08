using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
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
            using var reader = new DataReader(stream);
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

    public static byte[]? GenerateQrCode(string data, int pixelsPerModule = 10)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(pixelsPerModule);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
