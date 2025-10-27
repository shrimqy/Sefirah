using Sefirah.Data.Contracts;
using Sefirah.Data.Models;

namespace Sefirah.Platforms.Desktop.Services;
public class DesktopPlaybackService : IPlaybackService
{
    public Task HandleMediaActionAsync(PlaybackAction mediaAction)
    {
        return Task.CompletedTask;
    }

    public Task HandleRemotePlaybackMessageAsync(PlaybackSession data)
    {
        return Task.CompletedTask;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
