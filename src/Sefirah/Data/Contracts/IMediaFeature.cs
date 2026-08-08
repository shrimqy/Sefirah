using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

/// <summary>
/// Manages system media playback sessions
/// </summary>
public interface IMediaFeature : IFeature
{
    /// <summary>
    /// Executes a media session control action (play, pause, seek, etc.).
    /// </summary>
    Task HandleMediaActionAsync(MediaAction mediaAction);
}
