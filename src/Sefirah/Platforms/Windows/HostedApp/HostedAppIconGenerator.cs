using Windows.Graphics.Imaging;

namespace Sefirah.Platforms.Windows.HostedApp;

/// <summary>
/// Builds hosted-app image assets 
/// simple Fant resize via <see cref="BitmapEncoder.BitmapTransform"/> and duplicate variant filenames.
/// </summary>
internal static class HostedAppIconGenerator
{
    private static readonly string[] Logo44Variants =
    [
        "Square44x44Logo.png",
        "Square44x44Logo.targetsize-44.png",
        "Square44x44Logo.targetsize-44_altform-lightunplated.png",
        "Square44x44Logo.targetsize-44_altform-unplated.png",
        "StoreLogo.png"
    ];

    private static readonly string[] Logo150Variants =
    [
        "Square150x150Logo.png",
        "Square150x150Logo.targetsize-150.png",
        "Square150x150Logo.targetsize-150_altform-lightunplated.png",
        "Square150x150Logo.targetsize-150_altform-unplated.png",
        "Square150x150LogoUnpadded.png"
    ];

    public static async Task GenerateAsync(string sourceIconPath, string packageRoot, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceIconPath))
            throw new FileNotFoundException("App icon not found for hosted package registration.", sourceIconPath);

        var imagesPath = Path.Combine(packageRoot, "Images");
        var imagesEnUsPath = Path.Combine(imagesPath, "en-US");
        Directory.CreateDirectory(imagesEnUsPath);

        var logo44Bytes = await ResizePngBytesAsync(sourceIconPath, 44, cancellationToken);
        var logo150Bytes = await ResizePngBytesAsync(sourceIconPath, 150, cancellationToken);

        foreach (var fileName in Logo44Variants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WritePngBytesAsync(Path.Combine(imagesPath, fileName), logo44Bytes, cancellationToken);
            await WritePngBytesAsync(Path.Combine(imagesEnUsPath, fileName), logo44Bytes, cancellationToken);
        }

        foreach (var fileName in Logo150Variants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WritePngBytesAsync(Path.Combine(imagesPath, fileName), logo150Bytes, cancellationToken);

            if (!string.Equals(fileName, "Square150x150LogoUnpadded.png", StringComparison.Ordinal))
                await WritePngBytesAsync(Path.Combine(imagesEnUsPath, fileName), logo150Bytes, cancellationToken);
        }
    }

    /// <summary>
    /// Matches Phone Link <c>ResizeImageStreamAsync</c>: default software bitmap + encoder transform.
    /// </summary>
    private static async Task<byte[]> ResizePngBytesAsync(string sourceIconPath, int size, CancellationToken cancellationToken)
    {
        using var inputStream = File.OpenRead(sourceIconPath);
        using var outputStream = new MemoryStream();

        var decoder = await BitmapDecoder.CreateAsync(inputStream.AsRandomAccessStream());
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream.AsRandomAccessStream());
        encoder.SetSoftwareBitmap(await decoder.GetSoftwareBitmapAsync());
        encoder.BitmapTransform.ScaledWidth = (uint)size;
        encoder.BitmapTransform.ScaledHeight = (uint)size;
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        await encoder.FlushAsync();

        cancellationToken.ThrowIfCancellationRequested();
        return outputStream.ToArray();
    }

    private static async Task WritePngBytesAsync(string destinationPath, byte[] bytes, CancellationToken cancellationToken)
    {
        var tempPath = destinationPath + ".tmp";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);

            try
            {
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);

                File.Move(tempPath, destinationPath);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                try { File.Delete(tempPath); } catch { }
                await Task.Delay(100 * (attempt + 1), cancellationToken);
            }
        }

        await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken);
        try { File.Delete(tempPath); } catch { }
    }
}
