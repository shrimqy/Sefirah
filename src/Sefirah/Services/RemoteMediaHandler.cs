using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;

namespace Sefirah.Services;

public class RemoteMediaHandler : IRemoteMediaHandler
{
    private readonly SemaphoreSlim sessionLock = new(1, 1);

    public async Task HandleRemotePlaybackSessionAsync(PairedDevice device, PlaybackSession session)
    {
        await sessionLock.WaitAsync();
        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
            {
                switch (session.SessionType)
                {
                    case SessionType.PlaybackInfo:
                        await HandlePlaybackInfo(device, session);
                        break;
                    case SessionType.PlaybackUpdate:
                        HandlePlaybackUpdate(device, session);
                        break;
                    case SessionType.TimelineUpdate:
                        HandleTimelineUpdate(device, session);
                        break;
                    case SessionType.RemovedSession:
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

    private static async Task HandlePlaybackInfo(PairedDevice device, PlaybackSession session)
    {
        var existing = device.RemotePlaybackSessions.FirstOrDefault(s => s.Source == session.Source);
            
        if (existing is not null)
        {
            await existing.UpdateFrom(session);
        }
        else
        {
            var newSession = new MediaSession();
            await newSession.UpdateFrom(session);
            device.RemotePlaybackSessions.Add(newSession);
        }
    }

    private static void HandlePlaybackUpdate(PairedDevice device, PlaybackSession session)
    {
        var existing = device.RemotePlaybackSessions.FirstOrDefault(s => s.Source == session.Source);
        if (existing is not null)
        {
            existing.IsPlaying = session.IsPlaying;
            if (session.Position.HasValue)
            {
                existing.Position = session.Position.Value;
            }
        }
    }

    private static void HandleTimelineUpdate(PairedDevice device, PlaybackSession session)
    {
        var existing = device.RemotePlaybackSessions.FirstOrDefault(s => s.Source == session.Source);
        if (existing is not null && session.Position.HasValue)
        {
            existing.Position = session.Position.Value;
        }
    }

    private static void HandleRemovedSession(PairedDevice device, PlaybackSession session)
    {
        var sessionToRemove = device.RemotePlaybackSessions.FirstOrDefault(s => s.Source == session.Source);
        if (sessionToRemove is not null)
        {
            device.RemotePlaybackSessions.Remove(sessionToRemove);
        }
    }
}
