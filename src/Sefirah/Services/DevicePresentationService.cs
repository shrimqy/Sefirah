using Sefirah.Data.Models;
using Sefirah.Utils;
using CommunityToolkit.WinUI;

namespace Sefirah.Services;

public sealed class DevicePresentationService(IUserSettingsService settings)
{
    private readonly SemaphoreSlim imageGate = new(1, 1);
    private string? cachedPath;
    private DateTime cachedWrite;
    private byte[]? cachedImage;

    public async Task<DevicePresentation> GetAsync(PairedDevice? peer, bool includeImage = false)
    {
        var prefs = settings.GeneralSettingsService;
        var presentation = new DevicePresentation
        {
            Kind = prefs.PresentationKind,
            Orientation = prefs.PresentationOrientation,
            AspectRatio = prefs.PresentationRatio
        };
        if (!presentation.IsValid) presentation = new DevicePresentation { Kind = "computer" };
        if (!prefs.ShareWallpaperPreview || peer is not null && !peer.DeviceSettings.ShareWallpaperPreview)
            return presentation;
        await imageGate.WaitAsync();
        try
        {
            var path = prefs.PresentationImagePath;
            var manual = !string.IsNullOrEmpty(path);
            if (!manual)
                path = await App.MainWindow.DispatcherQueue.EnqueueAsync(WallpaperPreviewProvider.GetWallpaperPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return presentation;
            var write = File.GetLastWriteTimeUtc(path);
            if (path != cachedPath || write != cachedWrite)
            {
                cachedImage = await WallpaperPreviewProvider.CreateThumbnailAsync(path);
                cachedPath = path;
                cachedWrite = write;
            }
            if (cachedImage is null) return presentation;
            presentation.WallpaperSource = manual ? "userSelected" : "wallpaper";
            presentation.WallpaperId = PresentationImage.Id(cachedImage);
            if (includeImage) presentation.Wallpaper = Convert.ToBase64String(cachedImage);
        }
        catch (Exception) { /* Access/dynamic wallpaper failure is a deliberate no-image fallback. */ }
        finally { imageGate.Release(); }
        return presentation;
    }

    public async Task PublishAsync(PairedDevice peer, string? requestedId = null)
    {
        if (!peer.IsConnected) return;
        var local = await Ioc.Default.GetRequiredService<IDeviceManager>().GetLocalDeviceAsync();
        var presentation = await GetAsync(peer, requestedId is not null);
        if (requestedId is not null && requestedId != presentation.WallpaperId) return;
        peer.SendMessage(new DeviceInfo { DeviceName = local.DeviceName, Presentation = presentation });
    }

    public async Task PublishAllAsync()
    {
        foreach (var peer in Ioc.Default.GetRequiredService<IDeviceManager>().PairedDevices.ToArray())
            await PublishAsync(peer);
    }
}
