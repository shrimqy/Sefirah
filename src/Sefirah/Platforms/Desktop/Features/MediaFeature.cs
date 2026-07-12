using Sefirah.Data.Models;
using Sefirah.Platforms.Desktop.Services;

namespace Sefirah.Platforms.Desktop.Features;

public sealed class MediaFeature(
    ILogger<MediaFeature> logger,
    ILoggerFactory loggerFactory,
    ISessionManager sessionManager,
    IDeviceManager deviceManager) : IMediaFeature
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task HandleMediaActionAsync(MediaAction mediaAction)
    {

        return Task.CompletedTask;
    }
}
