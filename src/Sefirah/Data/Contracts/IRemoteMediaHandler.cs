using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

/// <summary>
/// Manages remote playback sessions received from Android devices.
/// Handles playback session updates and storage.
/// </summary>
public interface IRemoteMediaHandler
{
    /// <summary>
    /// Handles a playback session update from a remote device.
    /// </summary>
    /// <param name="device">The device that sent the playback session update.</param>
    /// <param name="session">The playback session data.</param>
    Task HandleRemotePlaybackSessionAsync(PairedDevice device, PlaybackSession session);
}
