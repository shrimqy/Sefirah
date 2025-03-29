using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Utils;
using Renci.SshNet;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using Sefirah.App.Utils.Serialization;
using System.Runtime.InteropServices;
using Windows.Media.Control;

namespace Sefirah.App.Services;

public class PlaybackService(
    ILogger logger,
    ISessionManager sessionManager) : IPlaybackService, IMMNotificationClient
{
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> activeSessions = [];
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    public List<AudioDevice> AudioDevices { get; private set; } = [];
    private readonly MMDeviceEnumerator enumerator = new();

    private readonly Dictionary<string, double> lastTimelinePosition = [];

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

            GetAllAudioDevices();
            UpdateActiveSessions();

            enumerator.RegisterEndpointNotificationCallback(this);

            manager.SessionsChanged += SessionsChanged;

            sessionManager.ClientConnectionStatusChanged += async (sender, args) =>
            {
                if (args.IsConnected && AudioDevices.Count != 0)
                {
                    foreach (var device in AudioDevices)
                    {
                        device.AudioDeviceType = AudioMessageType.New;
                        string jsonMessage = SocketMessageSerializer.Serialize(device);
                        sessionManager.SendMessage(jsonMessage);
                    }
                    foreach (var session in activeSessions.Values)
                    {
                        await UpdatePlaybackDataAsync(session);
                    }
                } 
            };

            logger.Info("PlaybackService initialized successfully");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize PlaybackService", ex);
            throw;
        }
    }

    public async Task HandleMediaActionAsync(PlaybackAction mediaAction)
    {
        var session = activeSessions.Values.FirstOrDefault(s => s.SourceAppUserModelId == mediaAction.Source);

        await ExecuteSessionActionAsync(session, mediaAction);
    }

    private async Task ExecuteSessionActionAsync(GlobalSystemMediaTransportControlsSession? session, PlaybackAction mediaAction)
    {
        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                switch (mediaAction.PlaybackActionType)
                {
                    case PlaybackActionType.Play:
                        await session?.TryPlayAsync();
                        break;
                    case PlaybackActionType.Pause:
                        await session?.TryPauseAsync();
                        break;
                    case PlaybackActionType.Next:
                        await session?.TrySkipNextAsync();
                        break;
                    case PlaybackActionType.Previous:
                        await session?.TrySkipPreviousAsync();
                        break;
                    case PlaybackActionType.Seek:
                        if (mediaAction.Value != null)  
                        {
                            // We need to use Ticks here
                            TimeSpan position = TimeSpan.FromMilliseconds(mediaAction.Value.Value);
                            await session?.TryChangePlaybackPositionAsync(position.Ticks);
                        }
                        break;
                    case PlaybackActionType.Shuffle:
                        await session?.TryChangeShuffleActiveAsync(true);
                        break;
                    case PlaybackActionType.Repeat:
                        if (mediaAction.Value != null)
                        {
                            if (mediaAction.Value == 1.0)
                            {
                                await session?.TryChangeAutoRepeatModeAsync(Windows.Media.MediaPlaybackAutoRepeatMode.Track);
                            }
                            else if (mediaAction.Value == 2.0)
                            {
                                await session?.TryChangeAutoRepeatModeAsync(Windows.Media.MediaPlaybackAutoRepeatMode.List);
                            }
                        }
                        break;
                    case PlaybackActionType.DefaultDevice:
                        SetDefaultAudioDevice(mediaAction.Source);
                        break;
                    case PlaybackActionType.VolumeUpdate:
                        if (mediaAction.Value != null)
                        {
                            SetVolume(mediaAction.Source, Convert.ToSingle(mediaAction.Value.Value));
                        }
                        break;
                    default:
                        logger.Warn("Unhandled media action: {0}", mediaAction.PlaybackActionType);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error executing media action {0} ", mediaAction.PlaybackActionType, ex);
                throw;
            }
        });
    }

    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager manager, SessionsChangedEventArgs args)
    {
        logger.Warn("Sessions changed");
        UpdateSessionsList(manager.GetSessions());
    }

    private void UpdateActiveSessions()
    {
        if (manager == null) return;

        try
        {
            var activeSessions = manager.GetSessions();
            UpdateSessionsList(activeSessions);
        }
        catch (Exception ex)
        {
            logger.Error("Error updating active sessions", ex);
        }
    }

    private void UpdateSessionsList(IReadOnlyList<GlobalSystemMediaTransportControlsSession> activeSessions)
    {
        lock (this.activeSessions)
        {
            var currentSessionIds = new HashSet<string>(activeSessions.Select(s => s.SourceAppUserModelId));
            
            foreach (var sessionId in this.activeSessions.Keys.ToList())
            {
                if (!currentSessionIds.Contains(sessionId))
                {
                    RemoveSession(sessionId);
                }
            }

            foreach (var session in activeSessions.Where(s => s != null))
            {
                if (!this.activeSessions.ContainsKey(session.SourceAppUserModelId))
                {
                    AddSession(session);
                }
            }
        }
    }

    private void RemoveSession(string sessionId)
    {
        if (activeSessions.TryGetValue(sessionId, out var session))
        {
            activeSessions.Remove(sessionId);
            UnsubscribeFromSessionEvents(session);
        }
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
            if (!activeSessions.ContainsKey(sender.SourceAppUserModelId))
            {
                logger.Info("Ignoring timeline properties change for removed session: {0}", sender.SourceAppUserModelId);
                return;
            }
            
            var timelineProperties = sender.GetTimelineProperties();
            var isCurrentSession = manager?.GetCurrentSession()?.SourceAppUserModelId == sender.SourceAppUserModelId;
            
            if (timelineProperties == null)
            {
                logger.Debug("Null timeline properties for {0}", sender.SourceAppUserModelId);
                return;
            }

            if (!isCurrentSession)
            {
                return;
            }
            
            if (lastTimelinePosition.TryGetValue(sender.SourceAppUserModelId, out var lastPosition))
            {
                double currentPosition = timelineProperties.Position.TotalMilliseconds;
                if (Math.Abs(currentPosition - lastPosition) < 1000)
                {
                    return;
                }
                
                lastTimelinePosition[sender.SourceAppUserModelId] = currentPosition;
                
                var message = new PlaybackSession
                {
                    SessionType = SessionType.TimelineUpdate,
                    Source = sender.SourceAppUserModelId,
                    IsCurrentSession = isCurrentSession,
                    Position = currentPosition
                };
                SendPlaybackData(message);
            }
        }
        catch (COMException ex)
        {
            logger.Error("COM Exception in timeline properties for {0}: {1}", sender.SourceAppUserModelId, ex.Message);
        }
        catch (Exception ex)
        {
            logger.Error("Error processing timeline properties for {0}: {1}", sender.SourceAppUserModelId, ex.Message);
        }
    }

    private void UnsubscribeFromSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
        session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
        session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
        lastTimelinePosition.Remove(session.SourceAppUserModelId);

        var message = new PlaybackSession
        {
            SessionType = SessionType.RemovedSession,
            Source = session.SourceAppUserModelId
        };
        logger.Debug("Removing session {0}", session.SourceAppUserModelId);
        SendPlaybackData(message);
    }

    private async void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        try
        {
            logger.Info("Media properties changed for {0}", sender.SourceAppUserModelId);
            await UpdatePlaybackDataAsync(sender);
            
        }
        catch (Exception ex)
        {
            logger.Error("Error updating playback data for {0}", sender.SourceAppUserModelId, ex);
        }
    }

    private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        try
        {
            logger.Debug("Playback info changed for {0}", sender.SourceAppUserModelId);
            var playbackInfo = sender.GetPlaybackInfo();
            var message = new PlaybackSession
            {
                SessionType = SessionType.PlaybackInfoUpdate,
                Source = sender.SourceAppUserModelId,
                IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                PlaybackRate = playbackInfo.PlaybackRate,
                IsShuffleActive = playbackInfo.IsShuffleActive,
            };

            SendPlaybackData(message);
        }
        catch (Exception ex)
        {
            logger.Error("Error updating playback data for {0}", sender.SourceAppUserModelId, ex);
        }
    }

    private async Task UpdatePlaybackDataAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            await dispatcher.EnqueueAsync(async () =>
            {

                var playbackSession = await GetPlaybackSessionAsync(session);

                if (playbackSession == null || !activeSessions.ContainsKey(session.SourceAppUserModelId) || !sessionManager.IsConnected())
                {
                    return;
                }
                SendPlaybackData(playbackSession);
            });
        }
        catch (Exception ex)
        {
            logger.Error("Error updating playback data for {0}", session.SourceAppUserModelId, ex);
        }
    }

    private async Task<PlaybackSession?> GetPlaybackSessionAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var mediaProperties = await session.TryGetMediaPropertiesAsync();
            var timelineProperties = session.GetTimelineProperties();
            var playbackInfo = session.GetPlaybackInfo();

            lastTimelinePosition[session.SourceAppUserModelId] = timelineProperties.Position.TotalMilliseconds;

            var currentSession = manager?.GetCurrentSession();

            var playbackSession = new PlaybackSession
            {
                Source = session.SourceAppUserModelId,
                TrackTitle = mediaProperties.Title,
                Artist = mediaProperties.Artist ?? "Unknown Artist",
                IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                IsShuffleActive = playbackInfo.IsShuffleActive,
                IsCurrentSession = currentSession?.SourceAppUserModelId == session.SourceAppUserModelId,
                PlaybackRate = playbackInfo.PlaybackRate,
                Position = timelineProperties?.Position.TotalMilliseconds,
                MinSeekTime = timelineProperties?.MinSeekTime.TotalMilliseconds,
                MaxSeekTime = timelineProperties?.MaxSeekTime.TotalMilliseconds
            };

            if (mediaProperties.Thumbnail != null)
            {
                playbackSession.Thumbnail = await ImageHelper.ToBase64Async(mediaProperties.Thumbnail);
            }

            return playbackSession;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting playback data for {0}", session.SourceAppUserModelId, ex);
            return null;
        }
    }


    private void SendPlaybackData(PlaybackSession playbackSession)
    {
        try
        {
            if (!sessionManager.IsConnected())
            {
                return;
            }
            string jsonMessage = SocketMessageSerializer.Serialize(playbackSession);
            sessionManager.SendMessage(jsonMessage);
        }
        catch (Exception ex)
        {
            logger.Error("Error sending playback data", ex);
        }
    }

    public void VolumeControlAsync(double volume)
    {
        try
        {

        }
        catch (Exception ex)
        {
            logger.Error("Error changing volume to {0}", volume, ex);
            throw;
        }
    }

    public Task HandleRemotePlaybackMessageAsync(PlaybackSession data)
    {
        throw new NotImplementedException();
    }

    public void GetAllAudioDevices()
    {
        // Get the default device
        var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        // List all active devices
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in devices)
        {
            AudioDevices.Add(
                new AudioDevice
                {
                    DeviceId = device.ID,
                    DeviceName = device.FriendlyName,
                    Volume = device.AudioEndpointVolume.MasterVolumeLevelScalar,
                    IsSelected = device.ID == defaultDevice.ID
                }
            );
        }
    }

    public void SetVolume(string deviceId, float volume)
    {
        try
        {
            var endpoint = enumerator.GetDevice(deviceId);
            if (endpoint == null) {
                logger.Warn("Endpoint not found: {0}", deviceId);
                return;
            }
            
            // Check if device is active
            if (endpoint.State != DeviceState.Active)
            {
                logger.Warn("Device {0} is not in active state: {1}", deviceId, endpoint.State);
                return;
            }
            
            try
            {
                endpoint.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
                logger.Info("Volume set to {0} for device {1}", volume, deviceId);
            }
            catch (COMException comEx) when (comEx.HResult == unchecked((int)0x8007001F))
            {
                logger.Warn("Device {0} not functioning when setting volume.", deviceId);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error setting volume to {0} for device {1}", volume, deviceId, ex);
        }
    }


    public void SetDefaultAudioDevice(string deviceId)
    {

        object? policyConfigObject = null;

        try
        {
            Type? policyConfigType = Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"));
            if (policyConfigType == null)
            {
                logger.Debug("Failed to get PolicyConfig type");
                return;
            }

            policyConfigObject = Activator.CreateInstance(policyConfigType);
            if (policyConfigObject == null)
            {
                return;
            }

            if (policyConfigObject is not IPolicyConfig policyConfig)
            {
                return;
            }

            int result1 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
            int result2 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
            int result3 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);

            if (result1 != (int)HResult.S_OK || result2 != (int)HResult.S_OK || result3 != (int)HResult.S_OK)
            {
                logger.Debug($"SetDefaultEndpoint returned error code: {result1}, {result2}, {result3}");
                return;
            }

            var index = AudioDevices.FindIndex(d => d.DeviceId == deviceId);

            if (index != -1)
            {
                AudioDevices.First().IsSelected = false;
                AudioDevices[index].IsSelected = true;
            }

            logger.Info("Successfully set default device");
            return;
        }
        catch (Exception ex)
        {
            logger.Error($"Error setting default device: {ex.Message}");
            return;
        }
        finally
        {
            if (policyConfigObject != null)
            {
                Marshal.ReleaseComObject(policyConfigObject);
            }
        }
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        logger.Info($"Device state changed: {deviceId} - {newState}");
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        AudioDevices.Add(
            new AudioDevice
            {
                AudioDeviceType = AudioMessageType.New,
                DeviceId = pwstrDeviceId,
                DeviceName = enumerator.GetDevice(pwstrDeviceId).FriendlyName,
                Volume = enumerator.GetDevice(pwstrDeviceId).AudioEndpointVolume.MasterVolumeLevelScalar,
                IsSelected = false
            }
        );
        logger.Info($"Device added: {pwstrDeviceId}");
    }

    public void OnDeviceRemoved(string deviceId)
    {
        AudioDevices.RemoveAll(d => d.DeviceId == deviceId);
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        var index = AudioDevices.FindIndex(d => d.DeviceId == defaultDeviceId);

        if (index != -1)
        {
            var selectedIndex = AudioDevices.FindIndex(d => d.IsSelected == true);
            AudioDevices[selectedIndex].IsSelected = false;
            AudioDevices[index].IsSelected = true;
            logger.Info($"Default device changed: {defaultDeviceId}");
        }
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        AudioDevice? device = AudioDevices.FirstOrDefault(d => d.DeviceId == pwstrDeviceId);
        if (device != null)
        {
            device.Volume = enumerator.GetDevice(pwstrDeviceId).AudioEndpointVolume.MasterVolumeLevelScalar;
        }
        logger.Info($"Property value changed: {pwstrDeviceId} - {key}");
    }
}