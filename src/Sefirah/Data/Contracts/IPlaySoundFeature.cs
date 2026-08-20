using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

/// <summary>
/// Plays a locating sound on a paired device
/// </summary>
public interface IPlaySoundFeature : IFeature
{
    void Toggle(PairedDevice device);

    void HandleRemoteState(PairedDevice device, bool isPlaying);
}
