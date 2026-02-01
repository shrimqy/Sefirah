using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Helpers;

namespace Sefirah.Data.Models;

public partial class MediaSession : ObservableObject
{
    private readonly DispatcherTimer positionUpdateTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    [ObservableProperty]
    public partial string? Source { get; set; }

    [ObservableProperty]
    public partial string? TrackTitle { get; set; }

    [ObservableProperty]
    public partial string? Artist { get; set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial bool? IsShuffleActive { get; set; }

    [ObservableProperty]
    public partial int? RepeatMode { get; set; }

    [ObservableProperty]
    public partial double? PlaybackRate { get; set; }

    [ObservableProperty]
    public partial double Position { get; set; }

    [ObservableProperty]
    public partial double MaxSeekTime { get; set; }
        
    [ObservableProperty]
    public partial double MinSeekTime { get; set; }

    [ObservableProperty]
    public partial BitmapImage? Thumbnail { get; set; }

    [ObservableProperty]
    public partial string? AppName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumePercentText))]
    public partial int Volume { get; set; }

    public string VolumePercentText => $"{Volume}%";

    [ObservableProperty]
    public partial bool? CanPlay { get; set; }

    [ObservableProperty]
    public partial bool? CanPause { get; set; }

    [ObservableProperty]
    public partial bool? CanGoNext { get; set; }

    [ObservableProperty]
    public partial bool? CanGoPrevious { get; set; }

    [ObservableProperty]
    public partial bool? CanSeek { get; set; }

    [ObservableProperty]
    public partial bool IsVolumeSliderVisible { get; set; }

    public MediaSession()
    {
        positionUpdateTimer.Tick += (s, e) => UpdatePosition();
        positionUpdateTimer.Start();
    }

    public async Task UpdateFrom(PlaybackSession session)
    {
        Source = session.Source;
        TrackTitle = session.TrackTitle;
        Artist = session.Artist;
        IsPlaying = session.IsPlaying;
        IsShuffleActive = session.IsShuffleActive;
        RepeatMode = session.RepeatMode;
        PlaybackRate = session.PlaybackRate;
        Position = session.Position ?? 0;
        MaxSeekTime = session.MaxSeekTime ?? 0;
        MinSeekTime = session.MinSeekTime ?? 0;
        AppName = session.AppName;
        Volume = session.Volume ;
        CanPlay = session.CanPlay;
        CanPause = session.CanPause;
        CanGoNext = session.CanGoNext;
        CanGoPrevious = session.CanGoPrevious;
        CanSeek = session.CanSeek;

        if (!string.IsNullOrEmpty(session.Thumbnail))
        {
            Thumbnail = await Convert.FromBase64String(session.Thumbnail).ToBitmapAsync();
        }
    }

    public void UpdatePosition()
    {
        if (!IsPlaying) return;
        if (MaxSeekTime <= 0) return;

        var newPosition = Position + 1000.0;
        if (newPosition >= MaxSeekTime)
        {
            newPosition = MaxSeekTime;
            // Stop updating when we reach the end
            return;
        }

        App.MainWindow.DispatcherQueue.EnqueueAsync(() => Position = newPosition);
    }
}
