using System.Runtime.InteropServices;
using System.Text;
#if WINDOWS
using Windows.Graphics.Imaging;
#else
using SkiaSharp;
#endif

namespace Sefirah.Utils;

internal static partial class WallpaperPreviewProvider
{
    public static string? GetWallpaperPath()
    {
        try
        {
#if WINDOWS
            var path = new StringBuilder(32768);
            return SystemParametersInfo(0x0073, (uint)path.Capacity, path, 0) ? path.ToString() : null;
#else
            if (!OperatingSystem.IsMacOS()) return null;
            var workspace = Send(GetClass("NSWorkspace"), Selector("sharedWorkspace"));
            var screen = Send(GetClass("NSScreen"), Selector("mainScreen"));
            var url = SendArgument(workspace, Selector("desktopImageURLForScreen:"), screen);
            var path = Send(url, Selector("path"));
            return Marshal.PtrToStringUTF8(Send(path, Selector("UTF8String")));
#endif
        }
        catch (Exception) { return null; }
    }

    public static async Task<byte[]?> CreateThumbnailAsync(string path)
    {
        using var input = File.OpenRead(path);
        if (input.Length > 64 * 1024 * 1024) return null;
#if WINDOWS
        var decoder = await BitmapDecoder.CreateAsync(input.AsRandomAccessStream());
        if ((long)decoder.PixelWidth * decoder.PixelHeight > 32 * 1024 * 1024) return null;
        var scale = Math.Min(1d, PresentationImage.MaxEdge / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Max(1, (int)(decoder.PixelWidth * scale)),
            ScaledHeight = (uint)Math.Max(1, (int)(decoder.PixelHeight * scale))
        };
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
        using var output = new MemoryStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output.AsRandomAccessStream());
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, transform.ScaledWidth,
            transform.ScaledHeight, 96, 96, pixels.DetachPixelData());
        await encoder.FlushAsync();
        var bytes = output.ToArray();
#else
        using var codec = SKCodec.Create(input);
        if (codec is null || (long)codec.Info.Width * codec.Info.Height > 32 * 1024 * 1024) return null;
        using var image = SKBitmap.Decode(codec);
        if (image is null) return null;
        var scale = Math.Min(1d, PresentationImage.MaxEdge / (double)Math.Max(image.Width, image.Height));
        using var resized = image.Resize(new SKImageInfo(Math.Max(1, (int)(image.Width * scale)),
            Math.Max(1, (int)(image.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear));
        if (resized is null) return null;
        using var encoded = resized.Encode(SKEncodedImageFormat.Jpeg, 75);
        var bytes = encoded.ToArray();
        await Task.CompletedTask;
#endif
        return PresentationImage.IsValid(bytes) ? bytes : null;
    }

#if WINDOWS
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint count, StringBuilder value, uint flags);
#else
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr Selector(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendArgument(IntPtr receiver, IntPtr selector, IntPtr argument);
#endif
}
