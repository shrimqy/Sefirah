using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

/// <summary>
/// Manages system audio devices: enumeration, default device, volume, and mute sync.
/// </summary>
public interface IAudioFeature : IFeature
{
    /// <summary>
    /// Executes an audio control action (volume, mute, default device).
    /// </summary>
    Task HandleAudioActionAsync(AudioAction action);
}
