using Sefirah.Data.Contracts;
using Sefirah.Data.Models;

namespace Sefirah.Platforms.Desktop.Services;

public class DesktopMediaService : IMediaService
{
    public Task HandleMediaActionAsync(MediaAction mediaAction)
    {
        return Task.CompletedTask;
    }

    public Task HandleRemotePlaybackMessageAsync(PlaybackInfo data)
    {
        return Task.CompletedTask;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}

