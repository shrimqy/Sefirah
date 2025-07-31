using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

/// <summary>
/// Manages system-wide media playback monitoring and control.
/// Provides functionality to track active media sessions, handle playback changes,
/// and control media playback across different applications.
/// </summary>
public interface IPlaybackService
{
    Task InitializeAsync();

    /// <summary>
    /// Executes the corresponding media control action on the current device.
    /// </summary>
    /// <param name="action">The media action to execute.</param>
    Task HandleMediaActionAsync(PlaybackAction mediaAction);

    /// <summary>
    /// Handles a media playback message from the remote device.
    /// </summary>
    /// <param name="data">The playback data containing action details.</param>
    Task HandleRemotePlaybackMessageAsync(PlaybackSession data);
}

