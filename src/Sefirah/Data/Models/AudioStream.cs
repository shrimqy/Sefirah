using Sefirah.Data.Enums;

namespace Sefirah.Data.Models;

/// <summary>
/// Manages the collection of audio streams and applies updates from the device.
/// </summary>
public class Audio
{
    public IReadOnlyList<AudioStream> Streams { get; } =
    [
        new(AudioStreamType.Media),
        new(AudioStreamType.Ring),
        new(AudioStreamType.Notification),
        new(AudioStreamType.Alarm),
        new(AudioStreamType.VoiceCall)
    ];

    public void Update(AudioStreamType streamType, int level, int maxLevel)
    {
        var stream = Streams.FirstOrDefault(s => s.StreamType == streamType);
        if (stream is not null)
        {
            stream.Level = level;
            stream.MaxLevel = maxLevel;
        }
    }
}

/// <summary>
/// Represents the volume level for a single audio stream type (media, ring, etc.).
/// </summary>
public partial class AudioStream(AudioStreamType streamType) : ObservableObject
{
    public AudioStreamType StreamType { get; } = streamType;

    [ObservableProperty]
    public partial int Level { get; set; }

    [ObservableProperty]
    public partial int MaxLevel { get; set; } = 15;

    public string IconGlyph => StreamType switch
    {
        AudioStreamType.Media => "\uE8D6",
        AudioStreamType.Ring => "\uE717",
        AudioStreamType.Notification => "\uF2A3",
        AudioStreamType.Alarm => "\uE823",
        AudioStreamType.VoiceCall => "\uF715",
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
