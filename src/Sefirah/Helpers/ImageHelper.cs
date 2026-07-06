using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using Sefirah.Utils;
using Windows.Storage.Streams;

namespace Sefirah.Helpers;

public static class ImageHelper
{
    public static BitmapImage? ToBitmap(byte[]? data)
    {
        if (data is null || data.Length == 0)
            return null;

        try
        {
            var stream = new MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.SetSource(stream.AsRandomAccessStream());
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static BitmapImage? ToDisplayIcon(byte[]? largeIcon, string? deviceId, string? appPackage)
    {
        var largeIconBitmap = ToBitmap(largeIcon);
        if (largeIconBitmap is not null)
            return largeIconBitmap;

        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(appPackage))
            return null;

        var uri = IconUtils.GetAppIconUri(deviceId, appPackage);
        return uri is null ? null : new BitmapImage(uri);
    }

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

    public static async Task<Uri?> SaveToTemporaryFileAsync(byte[]? imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            return null;

        try
        {
            var fileName = $"toast_image_{Guid.NewGuid():N}.png";
            var file = await ApplicationData.Current.TemporaryFolder
                .CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            using var dataWriter = new DataWriter(stream);
            dataWriter.WriteBytes(imageBytes);
            await dataWriter.StoreAsync();
            return new Uri($"ms-appdata:///temp/{fileName}");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<Uri?> SaveToTemporaryFileFromBase64Async(string? base64)
    {
        if (string.IsNullOrEmpty(base64))
            return null;

        try
        {
            return await SaveToTemporaryFileAsync(Convert.FromBase64String(base64));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<Uri?> SaveStreamToTemporaryFileAsync(IRandomAccessStreamReference? source)
    {
        if (source is null)
            return null;

        try
        {
            using var stream = await source.OpenReadAsync();
            if (stream.Size == 0)
                return null;

            using var managedStream = stream.AsStreamForRead();
            using var memory = new MemoryStream();
            await managedStream.CopyToAsync(memory);
            return await SaveToTemporaryFileAsync(memory.ToArray());
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
            return Convert.ToBase64String(await ReadStreamBytesAsync(stream));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static async Task<byte[]> ReadStreamBytesAsync(IRandomAccessStream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.AsStream().CopyToAsync(memoryStream);
        return memoryStream.ToArray();
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
