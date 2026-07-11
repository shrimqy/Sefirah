using System.Runtime.InteropServices;
using SkiaSharp;

namespace Sefirah.Platforms.Desktop.Tray;

internal static class LinuxTrayIconLoader
{
    private static readonly int[] TrayIconSizes = [16, 22, 32];

    public static (int Width, int Height, byte[] Pixels)[] LoadPixmaps(string iconPath)
    {
        if (!File.Exists(iconPath))
            throw new FileNotFoundException($"Tray icon not found: {iconPath}", iconPath);

        return TrayIconSizes
            .Select(size => LoadPixmap(iconPath, size))
            .ToArray();
    }

    public static (int Width, int Height, byte[] Pixels) LoadPixmap(string iconPath, int size = 22)
    {
        using var stream = File.OpenRead(iconPath);
        using var original = SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException($"Failed to decode tray icon: {iconPath}");

        // SNI ARGB32 uses the host's native byte order (on Linux/x86: B,G,R,A in memory).
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var dest = new SKRect(0, 0, size, size);
            var src = new SKRect(0, 0, original.Width, original.Height);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(original, src, dest, paint);
        }

        var pixels = new byte[size * size * 4];
        if (bitmap.GetPixels() == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to read tray icon pixels: {iconPath}");

        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return (size, size, pixels);
    }

    public static string GetTrayIconPath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "SefirahDark.ico"));
}
