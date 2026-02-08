using Sefirah.Data.Enums;

namespace Sefirah.Data.Models;

/// <summary>
/// Represents the volume level for a single audio stream type (media, ring, etc.).
/// </summary>
public partial class AudioStream(AudioStreamType streamType) : ObservableObject
{
    public AudioStreamType StreamType { get; } = streamType;

    [ObservableProperty]
    public partial int Level { get; set; }

    public string IconGlyph => StreamType switch
    {
        AudioStreamType.Media => "\uE8D6",
        AudioStreamType.Ring => "\uE77E",
        AudioStreamType.Notification => "\uE91C",
        AudioStreamType.Alarm => "\uE823",
        AudioStreamType.VoiceCall => "\uE717",
        _ => "\uE767"
    };

    public string Label => StreamType switch
    {
        AudioStreamType.Media => "AudioMedia".GetLocalizedResource(),
        AudioStreamType.Ring => "AudioRing".GetLocalizedResource(),
        AudioStreamType.Notification => "AudioNotification".GetLocalizedResource(),
        AudioStreamType.Alarm => "AudioAlarm".GetLocalizedResource(),
        AudioStreamType.VoiceCall => "AudioCall".GetLocalizedResource(),
        _ => "Unknown"
    };
}
