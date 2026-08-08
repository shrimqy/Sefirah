using System.Collections.Concurrent;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Windows.Media;
using Windows.Media.Control;

namespace Sefirah.Platforms.Windows.Features;

public class MediaFeature(
    ILogger logger,
    ISessionManager sessionManager,
    IDeviceManager deviceManager) : IMediaFeature
{
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly ConcurrentDictionary<string, GlobalSystemMediaTransportControlsSession> activeSessions = [];
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private readonly Dictionary<string, double> lastTimelinePosition = [];

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;

        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager is null)
            {
                logger.Error("Failed to initialize GlobalSystemMediaTransportControlsSessionManager");
                return;
            }

            manager.SessionsChanged += SessionsChanged;
            UpdateSessionsList(manager.GetSessions());
            logger.Info("Media session manager initialized");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize media sessions", ex);
        }
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (!device.IsConnected || !device.DeviceSettings.MediaSessionSend) return;

        foreach (var session in activeSessions.Values)
            UpdatePlaybackDataAsync(session);
    }

    public Task HandleMediaActionAsync(MediaAction mediaAction)
    {
        activeSessions.TryGetValue(mediaAction.Source, out var session);

        return dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                switch (mediaAction.ActionType)
                {
                    case MediaActionType.Play:
                        await session?.TryPlayAsync();
                        break;
                    case MediaActionType.Pause:
                        await session?.TryPauseAsync();
                        break;
                    case MediaActionType.Next:
                        await session?.TrySkipNextAsync();
                        break;
                    case MediaActionType.Previous:
                        await session?.TrySkipPreviousAsync();
                        break;
                    case MediaActionType.Seek:
                        if (mediaAction.Value.HasValue)
                        {
                            TimeSpan position = TimeSpan.FromMilliseconds(mediaAction.Value.Value);
                            await session?.TryChangePlaybackPositionAsync(position.Ticks);
                        }
                        break;
                    case MediaActionType.Shuffle:
                        await session?.TryChangeShuffleActiveAsync(true);
                        break;
                    case MediaActionType.Repeat:
                        if (mediaAction.Value.HasValue)
                        {
                            if (mediaAction.Value == 1.0)
                                await session?.TryChangeAutoRepeatModeAsync(MediaPlaybackAutoRepeatMode.List);
                            else if (mediaAction.Value == 2.0)
                                await session?.TryChangeAutoRepeatModeAsync(MediaPlaybackAutoRepeatMode.Track);
                        }
                        break;
                    default:
                        logger.Warn($"Unhandled media action: {mediaAction.ActionType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error executing media action {mediaAction.ActionType}", ex);
            }
        });
    }

    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager manager, SessionsChangedEventArgs args)
    {
        UpdateSessionsList(manager.GetSessions());
    }

    private void UpdateSessionsList(IReadOnlyList<GlobalSystemMediaTransportControlsSession> activeSessions)
    {
        var currentSessionIds = new HashSet<string>(activeSessions.Select(s => s.SourceAppUserModelId));

        foreach (var sessionId in this.activeSessions.Keys.ToList())
        {
            if (!currentSessionIds.Contains(sessionId))
                RemoveSession(sessionId);
        }

        foreach (var session in activeSessions.Where(s => s is not null))
        {
            if (!this.activeSessions.ContainsKey(session.SourceAppUserModelId))
                AddSession(session);
        }
    }

    private void RemoveSession(string sessionId)
    {
        if (activeSessions.TryRemove(sessionId, out var session))
            UnsubscribeFromSessionEvents(session);
    }

    private void AddSession(GlobalSystemMediaTransportControlsSession session)
    {
        if (!activeSessions.ContainsKey(session.SourceAppUserModelId))
        {
            activeSessions[session.SourceAppUserModelId] = session;
            lastTimelinePosition[session.SourceAppUserModelId] = 0;
            SubscribeToSessionEvents(session);
        }
    }

    private void SubscribeToSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        session.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
        session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
        session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
    }

    private void Session_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        try
        {
            if (!activeSessions.ContainsKey(sender.SourceAppUserModelId)) return;
            var timelineProperties = sender.GetTimelineProperties();
            var isCurrentSession = manager?.GetCurrentSession()?.SourceAppUserModelId == sender.SourceAppUserModelId;

            if (timelineProperties is null || !isCurrentSession) return;

            if (lastTimelinePosition.TryGetValue(sender.SourceAppUserModelId, out var lastPosition))
            {
                double currentPosition = timelineProperties.Position.TotalMilliseconds;
                if (Math.Abs(currentPosition - lastPosition) < 1000) return;

                lastTimelinePosition[sender.SourceAppUserModelId] = currentPosition;

                SendPlaybackData(new PlaybackInfo
                {
                    InfoType = PlaybackInfoType.TimelineUpdate,
                    Source = sender.SourceAppUserModelId,
                    Position = currentPosition
                });
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error processing timeline properties for {sender.SourceAppUserModelId}", ex);
        }
    }

    private void UnsubscribeFromSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
        session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
        session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
        lastTimelinePosition.Remove(session.SourceAppUserModelId);

        SendPlaybackData(new PlaybackInfo
        {
            InfoType = PlaybackInfoType.RemovedSession,
            Source = session.SourceAppUserModelId
        });
    }

    private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args)
    {
        UpdatePlaybackDataAsync(session);
    }

    private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        try
        {
            var playbackInfo = sender.GetPlaybackInfo();
            SendPlaybackData(new PlaybackInfo
            {
                InfoType = PlaybackInfoType.PlaybackUpdate,
                Source = sender.SourceAppUserModelId,
                IsPlaying = playbackInfo.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                PlaybackRate = playbackInfo.PlaybackRate,
                IsShuffleActive = playbackInfo.IsShuffleActive,
            });
        }
        catch (Exception ex)
        {
            logger.Error($"Error updating playback data for {sender.SourceAppUserModelId}", ex);
        }
    }

    private async void UpdatePlaybackDataAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            await dispatcher.EnqueueAsync(async () =>
            {
                var playbackSession = await GetPlaybackSessionAsync(session);
                if (playbackSession is null || !activeSessions.ContainsKey(session.SourceAppUserModelId)) return;

                SendPlaybackData(playbackSession);
            });
        }
        catch (Exception ex)
        {
            logger.Error($"Error updating playback data for {session.SourceAppUserModelId}", ex);
        }
    }

    private async Task<PlaybackInfo?> GetPlaybackSessionAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var mediaProperties = await session.TryGetMediaPropertiesAsync();
            var timelineProperties = session.GetTimelineProperties();
            var playbackInfo = session.GetPlaybackInfo();

            if (playbackInfo is null) return null;

            lastTimelinePosition[session.SourceAppUserModelId] = timelineProperties.Position.TotalMilliseconds;

            var playbackSession = new PlaybackInfo
            {
                InfoType = PlaybackInfoType.PlaybackInfo,
                Source = session.SourceAppUserModelId,
                TrackTitle = mediaProperties.Title,
                Artist = mediaProperties.Artist ?? "Unknown Artist",
                IsPlaying = playbackInfo.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                IsShuffleActive = playbackInfo.IsShuffleActive,
                PlaybackRate = playbackInfo.PlaybackRate,
                Position = timelineProperties?.Position.TotalMilliseconds,
                MinSeekTime = timelineProperties?.MinSeekTime.TotalMilliseconds,
                MaxSeekTime = timelineProperties?.MaxSeekTime.TotalMilliseconds
            };

            if (mediaProperties.Thumbnail is not null)
                playbackSession.Thumbnail = await mediaProperties.Thumbnail.ToBase64Async();

            return playbackSession;
        }
        catch (Exception ex)
        {
            logger.Error($"Error getting playback data for {session.SourceAppUserModelId}", ex);
            return null;
        }
    }

    private void SendPlaybackData(PlaybackInfo playbackSession)
    {
        try
        {
            foreach (var device in deviceManager.PairedDevices)
            {
                if (device.IsConnected && device.DeviceSettings.MediaSessionSend)
                    device.SendMessage(playbackSession);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error sending playback data", ex);
        }
    }
}
