using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using Sefirah.App.Utils.Serialization;
using System.Runtime.InteropServices;
using Windows.Media.Control;

namespace Sefirah.App.Services;

public class PlaybackService(
    ILogger logger,
    ISessionManager sessionManager) : IPlaybackService,  IDisposable
{
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _activeSessions = [];
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private bool _disposed;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager == null)
            {
                logger.Error("Failed to initialize GlobalSystemMediaTransportControlsSessionManager");
                return;
            }

            manager.SessionsChanged += Manager_SessionsChanged;
            UpdateActiveSessions();

            logger.Info("PlaybackService initialized successfully");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize PlaybackService", ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task HandleLocalMediaActionAsync(PlaybackData request)
    {
        try
        {
            if (!Enum.TryParse(request.MediaAction, true, out MediaAction action)) return;

            await ExecuteMediaActionAsync(request, action);
        }
        catch (Exception ex)
        {
            logger.Error("Error handling media action", ex);
            throw;
        }
    }

    private async Task ExecuteMediaActionAsync(PlaybackData request, MediaAction action)
    {
        var session = _activeSessions.Values.FirstOrDefault();
        if (session == null)
        {
            logger.Warn("No active media sessions found");
            return;
        }

        await ExecuteSessionActionAsync(session, action, request);
    }

    private async Task ExecuteSessionActionAsync(GlobalSystemMediaTransportControlsSession session, MediaAction action, PlaybackData playbackData)
    {
        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                logger.Info("Executing {0} for session {1}", action, session.SourceAppUserModelId);

                switch (action)
                {
                    case MediaAction.Resume:
                        await session.TryPlayAsync();
                        break;
                    case MediaAction.Pause:
                        await session.TryPauseAsync();
                        break;
                    case MediaAction.NextQueue:
                        await session.TrySkipNextAsync();
                        break;
                    case MediaAction.PrevQueue:
                        await session.TrySkipPreviousAsync();
                        break;
                    case MediaAction.Volume:
                        VolumeControlAsync(playbackData.Volume);
                        break;
                    case MediaAction.Seek:
                        break;
                    default:
                        logger.Warn("Unhandled media action: {0}", action);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error executing media action {0} for session {1}",
                    new object[] { action, session.SourceAppUserModelId }, ex);
                throw;
            }
        });
    }

    private void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        UpdateActiveSessions();
    }

    private void UpdateActiveSessions()
    {
        if (manager == null) return;

        try
        {
            var currentSessions = manager.GetSessions();
            UpdateSessionsList(currentSessions);
            TriggerPlaybackDataUpdate();
        }
        catch (Exception ex)
        {
            logger.Error("Error updating active sessions", ex);
        }
    }

    private void UpdateSessionsList(IReadOnlyList<GlobalSystemMediaTransportControlsSession> currentSessions)
    {
        // Remove old sessions
        foreach (var sessionId in _activeSessions.Keys.ToList())
        {
            if (!currentSessions.Any(s => s.SourceAppUserModelId == sessionId))
            {
                RemoveSession(sessionId);
            }
        }

        // Add new sessions
        foreach (var session in currentSessions.Where(s => s != null))
        {
            AddSession(session);
        }
    }

    private void RemoveSession(string sessionId)
    {
        if (_activeSessions.TryGetValue(sessionId, out var session))
        {
            UnsubscribeFromSessionEvents(session);
            _activeSessions.Remove(sessionId);
        }
    }

    private void AddSession(GlobalSystemMediaTransportControlsSession session)
    {
        if (!_activeSessions.ContainsKey(session.SourceAppUserModelId))
        {
            _activeSessions[session.SourceAppUserModelId] = session;
            SubscribeToSessionEvents(session);
        }
    }

    private void SubscribeToSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
        session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
    }

    private void UnsubscribeFromSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
        session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
    }

    private async void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        logger.Debug("Media properties changed for {0}", sender.SourceAppUserModelId);
        await UpdatePlaybackDataAsync(sender);
    }

    private async void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        await UpdatePlaybackDataAsync(sender);
    }

    private async void TriggerPlaybackDataUpdate()
    {
        if (_activeSessions.Count == 0)
        {
            return;
        }

        var sessions = _activeSessions.Values.ToList();
    
        foreach (var session in sessions)
        {
            await UpdatePlaybackDataAsync(session);
        }
    }

    private async Task UpdatePlaybackDataAsync(GlobalSystemMediaTransportControlsSession session)
    {
        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                // Check if playback session and client session are still active before processing
                if (!_activeSessions.ContainsKey(session.SourceAppUserModelId) && !sessionManager.IsConnected())
                {
                    return;
                }

                var playbackData = await GetPlaybackDataAsync(session);
                if (playbackData != null)
                {
                    SendPlaybackData(playbackData);
                }
            }
            catch (COMException ex)
            {
                logger.Error("COM Exception updating playback data for {0}", session.SourceAppUserModelId, ex);
                await dispatcher.EnqueueAsync(() => _activeSessions.Remove(session.SourceAppUserModelId));
            }
            catch (Exception ex)
            {
                logger.Error("Error updating playback data for {0}", session.SourceAppUserModelId, ex);
            }
        });
    }

    private async Task<PlaybackData?> GetPlaybackDataAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var mediaProperties = await session.TryGetMediaPropertiesAsync();
            var timelineProperties = session.GetTimelineProperties();
            var playbackInfo = session.GetPlaybackInfo();

            if (mediaProperties == null || playbackInfo == null)
            {
                logger.Warn("Failed to get media properties or playback info for {SessionId}",
                    session.SourceAppUserModelId);
                return null;
            }

            var playbackData = new PlaybackData
            {
                AppName = session.SourceAppUserModelId,
                TrackTitle = mediaProperties.Title ?? "Unknown Title",
                Artist = mediaProperties.Artist ?? "Unknown Artist",
                IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                Position = timelineProperties?.Position.Ticks,
                MinSeekTime = timelineProperties?.MinSeekTime.Ticks,
                MaxSeekTime = timelineProperties?.MaxSeekTime.Ticks,
                Volume = VolumeControl.GetMasterVolume() * 100
            };

            if (mediaProperties.Thumbnail != null)
            {
                playbackData.Thumbnail = await ImageHelper.ToBase64Async(mediaProperties.Thumbnail);
            }

            return playbackData;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting playback data for {0}", session.SourceAppUserModelId, ex);
            return null;
        }
    }


    private void SendPlaybackData(PlaybackData playbackData)
    {
        try
        {
            string jsonMessage = SocketMessageSerializer.Serialize(playbackData);
            sessionManager.SendMessage(jsonMessage);
        }
        catch (Exception ex)
        {
            logger.Error("Error sending playback data", ex);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            foreach (var session in _activeSessions.Values)
            {
                UnsubscribeFromSessionEvents(session);
            }
            _activeSessions.Clear();

            if (manager != null)
            {
                manager.SessionsChanged -= Manager_SessionsChanged;
            }
        }
        _disposed = true;
    }

    public void VolumeControlAsync(double volume)
    {
        try
        {
            VolumeControl.ChangeVolume(volume);
            logger.Info("Volume changed to {0}", volume);
        }
        catch (Exception ex)
        {
            logger.Error("Error changing volume to {0}", volume, ex);
            throw;
        }
    }

    public Task HandleRemotePlaybackMessageAsync(PlaybackData data)
    {
        throw new NotImplementedException();
    }
}