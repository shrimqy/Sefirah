using Sefirah.Data.Models;

namespace Sefirah.Platforms.Desktop.Features;

/// <summary>
/// Desktop has no system media-session API equivalent to GSMTC yet.
/// Session actions are a no-op; audio lives in <see cref="AudioFeature"/>.
/// </summary>
public sealed class MediaFeature(ILogger<MediaFeature> logger) : IMediaFeature
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task HandleMediaActionAsync(MediaAction mediaAction)
    {
        logger.Debug($"Media session action ignored on desktop: {mediaAction.ActionType}");
        return Task.CompletedTask;
    }
}
