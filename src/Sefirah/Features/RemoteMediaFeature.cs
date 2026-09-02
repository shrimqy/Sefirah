using CommunityToolkit.WinUI;
using Sefirah.Data.Models;

namespace Sefirah.Features;

public class RemoteMediaFeature : IRemoteMediaFeature
{
    private readonly SemaphoreSlim sessionLock = new(1, 1);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task HandleRemotePlaybackSessionAsync(PairedDevice device, PlaybackInfo session)
    {
        if (!device.DeviceSettings.MediaSessionReceive) return;
        
        await sessionLock.WaitAsync();
        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
            {
                switch (session.InfoType)
                {
                    case PlaybackInfoType.PlaybackInfo:
                        await HandlePlaybackInfo(device, session);
                        break;
                    case PlaybackInfoType.PlaybackUpdate:
                        HandlePlaybackUpdate(device, session);
                        break;
                    case PlaybackInfoType.TimelineUpdate:
                        HandleTimelineUpdate(device, session);
                        break;
                    case PlaybackInfoType.RemovedSession:
                        HandleRemovedSession(device, session);
                        break;
                }
            });
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private static async Task HandlePlaybackInfo(PairedDevice device, PlaybackInfo session)
    {
        var mediaSession = device.RemotePlaybackSessions.FirstOrDefault(s => s.Source == session.Source);
            
        if (mediaSession is not null)
        {
            await mediaSession.UpdateFrom(session);
        }
        else
        {
            mediaSession = new MediaSession();
            await mediaSession.UpdateFrom(session);
            device.RemotePlaybackSessions.Add(mediaSession);
        }

        if (device.LastPlayingSession is null || session.IsPlaying)
            device.LastPlayingSession = mediaSession;
    }

    private static void HandlePlaybackUpdate(PairedDevice device, PlaybackInfo session)
    {
        var mediaSession = device.RemotePlaybackSessions.FirstOrDefault(s => s.Source == session.Source);
        if (mediaSession is null)
            return;

        mediaSession.IsPlaying = session.IsPlaying;
        if (session.Position.HasValue)
            mediaSession.Position = session.Position.Value;

        if (device.LastPlayingSession is null || session.IsPlaying)
            device.LastPlayingSession = mediaSession;
    }

    private static void HandleTimelineUpdate(PairedDevice device, PlaybackInfo session)
    {
        var existing = device.RemotePlaybackSessions.FirstOrDefault(s => s.Source == session.Source);
        if (existing is not null && session.Position.HasValue)
            existing.Position = session.Position.Value;
    }

    private static void HandleRemovedSession(PairedDevice device, PlaybackInfo session)
    {
        var sessionToRemove = device.RemotePlaybackSessions.FirstOrDefault(s => s.Source == session.Source);
        if (sessionToRemove is not null)
            device.RemotePlaybackSessions.Remove(sessionToRemove);
    }
}
