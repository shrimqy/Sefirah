using System.Runtime.InteropServices;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Utils;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Platforms.Windows.Interop;
using Windows.Media;
using Windows.Media.Control;

namespace Sefirah.Platforms.Windows.Services;

public class WindowsMediaService(
    ILogger<WindowsMediaService> logger,
    ISessionManager sessionManager,
    IDeviceManager deviceManager) : IMediaService, IMMNotificationClient
{
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> activeSessions = [];
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    public List<AudioDeviceInfo> AudioDevices { get; private set; } = [];
    private readonly MMDeviceEnumerator enumerator = new();
    private readonly Dictionary<string, DeviceVolumeNotificationHandler> deviceHandlers = [];

    private readonly Dictionary<string, double> lastTimelinePosition = [];

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager is null)
            {
                logger.LogError("Failed to initialize GlobalSystemMediaTransportControlsSessionManager");
                return;
            }

            GetAllAudioDevices();
            UpdateActiveSessions();

            enumerator.RegisterEndpointNotificationCallback(this);

            manager.SessionsChanged += SessionsChanged;

            sessionManager.ConnectionStatusChanged += async (sender, device) =>
            {
                if (device.IsConnected)
                {
                    if (device.DeviceSettings.MediaSessionSend)
                    {
                        foreach (var session in activeSessions.Values)
                        {
                            await UpdatePlaybackDataAsync(session);
                        }
                    }
                    if (device.DeviceSettings.AudioSync)
                    {
                        foreach (var audioDevice in AudioDevices)
                        {
                            audioDevice.InfoType = AudioInfoType.New;
                            device.SendMessage(audioDevice);
                        }
                    }
                }
            };

            logger.LogInformation("PlaybackService initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize PlaybackService");
        }
    }

    public async Task HandleMediaActionAsync(MediaAction mediaAction)
    {
        var session = activeSessions.Values.FirstOrDefault(s => s.SourceAppUserModelId == mediaAction.Source);

        await ExecuteSessionActionAsync(session, mediaAction);
    }

    private async Task ExecuteSessionActionAsync(GlobalSystemMediaTransportControlsSession? session, MediaAction mediaAction)
    {
        await dispatcher.EnqueueAsync(async () =>
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
                            // We need to use Ticks here
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
                            {
                                await session?.TryChangeAutoRepeatModeAsync(MediaPlaybackAutoRepeatMode.List);
                            }
                            else if (mediaAction.Value == 2.0)
                            {
                                await session?.TryChangeAutoRepeatModeAsync(MediaPlaybackAutoRepeatMode.Track);
                            }
                        }
                        break;
                    case MediaActionType.DefaultDevice:
                        SetDefaultAudioDevice(mediaAction.Source);
                        break;
                    case MediaActionType.VolumeUpdate:
                        if (mediaAction.Value.HasValue)
                        {
                            SetVolume(mediaAction.Source, Convert.ToSingle(mediaAction.Value.Value));
                        }
                        break;
                    case MediaActionType.ToggleMute:
                        ToggleMute(mediaAction.Source);
                        break;
                    default:
                        logger.LogWarning("Unhandled media action: {PlaybackActionType}", mediaAction.ActionType);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing media action {PlaybackActionType}", mediaAction.ActionType);
            }
        });
    }

    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager manager, SessionsChangedEventArgs args)
    {
        UpdateSessionsList(manager.GetSessions());
    }

    private void UpdateActiveSessions()
    {
        if (manager is null) return;

        try
        {
            var activeSessions = manager.GetSessions();
            UpdateSessionsList(activeSessions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating active sessions");
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

            foreach (var session in activeSessions.Where(s => s is not null))
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
            if (!activeSessions.ContainsKey(sender.SourceAppUserModelId)) return;
            var timelineProperties = sender.GetTimelineProperties();
            var isCurrentSession = manager?.GetCurrentSession()?.SourceAppUserModelId == sender.SourceAppUserModelId;

            if (timelineProperties is null || !isCurrentSession) return;

            if (lastTimelinePosition.TryGetValue(sender.SourceAppUserModelId, out var lastPosition))
            {
                double currentPosition = timelineProperties.Position.TotalMilliseconds;
                if (Math.Abs(currentPosition - lastPosition) < 1000) return; // Ignore minor changes under 1 second

                lastTimelinePosition[sender.SourceAppUserModelId] = currentPosition;

                var playbackSession = new PlaybackInfo
                {
                    InfoType = PlaybackInfoType.TimelineUpdate,
                    Source = sender.SourceAppUserModelId,
                    Position = currentPosition
                };
                SendPlaybackData(playbackSession);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing timeline properties for {SourceAppUserModelId}", sender.SourceAppUserModelId);
        }
    }

    private void UnsubscribeFromSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
        session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
        session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
        lastTimelinePosition.Remove(session.SourceAppUserModelId);

        var playbackSession = new PlaybackInfo
        {
            InfoType = PlaybackInfoType.RemovedSession,
            Source = session.SourceAppUserModelId
        };
        SendPlaybackData(playbackSession);
    }

    private async void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args)
    {
        try
        {
            logger.LogInformation("Media properties changed for {SourceAppUserModelId}", session.SourceAppUserModelId);
            await UpdatePlaybackDataAsync(session);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating playback data for {SourceAppUserModelId}", session.SourceAppUserModelId);
        }
    }

    private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        try
        {
            var playbackInfo = sender.GetPlaybackInfo();
            var playbackSession = new PlaybackInfo
            {
                InfoType = PlaybackInfoType.PlaybackUpdate,
                Source = sender.SourceAppUserModelId,
                IsPlaying = playbackInfo.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                PlaybackRate = playbackInfo.PlaybackRate,
                IsShuffleActive = playbackInfo.IsShuffleActive,
            };

            SendPlaybackData(playbackSession);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating playback data for {SourceAppUserModelId}", sender.SourceAppUserModelId);
        }
    }

    private async Task UpdatePlaybackDataAsync(GlobalSystemMediaTransportControlsSession session)
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
            logger.LogError(ex, "Error updating playback data for {SourceAppUserModelId}", session.SourceAppUserModelId);
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
            logger.LogError(ex, "Error getting playback data for {SourceAppUserModelId}", session.SourceAppUserModelId);
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
                {
                    device.SendMessage(playbackSession);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending playback data");
        }
    }

    public void GetAllAudioDevices()
    {
        try
        {
            // Get the default device
            var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;

            // List all active devices
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                AudioDevices.Add(
                    new AudioDeviceInfo
                    {
                        DeviceId = device.ID,
                        DeviceName = device.FriendlyName,
                        Volume = device.AudioEndpointVolume.MasterVolumeLevelScalar,
                        IsMuted = device.AudioEndpointVolume.Mute,
                        IsSelected = device.ID == defaultDevice
                    }
                );

                var handler = new DeviceVolumeNotificationHandler(device.ID, device);
                handler.SetHandleAction(OnDeviceVolumeChanged);
                device.AudioEndpointVolume.OnVolumeNotification += handler.Handle;
                deviceHandlers[device.ID] = handler;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate audio devices");
        }
    }

    private void OnDeviceVolumeChanged(string deviceId, AudioVolumeNotificationData data)
    {
        try
        {
            var audioDevice = AudioDevices.FirstOrDefault(d => d.DeviceId == deviceId);
            if (audioDevice is null) return;

            audioDevice.Volume = data.MasterVolume;
            audioDevice.IsMuted = data.Muted;
            audioDevice.InfoType = AudioInfoType.Active;

            SendAudioDeviceUpdate(audioDevice);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling volume change for device {DeviceId}", deviceId);
        }
    }

    private void SendAudioDeviceUpdate(AudioDeviceInfo audioDevice)
    {
        try
        {
            foreach (var device in deviceManager.PairedDevices)
            {
                if (device.IsConnected && device.DeviceSettings.AudioSync)
                {
                    device.SendMessage(audioDevice);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending audio device update");
        }
    }

    public void ToggleMute(string deviceId)
    {
        try
        {
            var endpoint = enumerator.GetDevice(deviceId);
            if (endpoint is null || endpoint.State is not DeviceState.Active) return;

            try
            {
                endpoint.AudioEndpointVolume.Mute = !endpoint.AudioEndpointVolume.Mute;
            }
            catch (COMException comEx) when (comEx.HResult == unchecked((int)0x8007001F))
            {
                logger.LogWarning("Device {DeviceId} not functioning when muting", deviceId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error muting device {DeviceId}", deviceId);
        }
    }

    public void SetVolume(string deviceId, float volume)
    {
        try
        {
            var endpoint = enumerator.GetDevice(deviceId);
            if (endpoint is null || endpoint.State is not DeviceState.Active) return;

            try
            {
                endpoint.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
            }
            catch (COMException comEx) when (comEx.HResult == unchecked((int)0x8007001F))
            {
                logger.LogWarning("Device {DeviceId} not functioning when setting volume", deviceId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting volume to {Volume} for device {DeviceId}", volume, deviceId);
        }
    }


    public void SetDefaultAudioDevice(string deviceId)
    {
        object? policyConfigObject = null;
        try
        {
            Type? policyConfigType = Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"));
            if (policyConfigType is null) return; 

            policyConfigObject = Activator.CreateInstance(policyConfigType);
            if (policyConfigObject is null) return;

            if (policyConfigObject is not IPolicyConfig policyConfig) return;

            int result1 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
            int result2 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
            int result3 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);

            if (result1 is not HResult.S_OK || result2 is not HResult.S_OK || result3 is not HResult.S_OK)
            {
                logger.LogError("SetDefaultEndpoint returned error codes: {Result1}, {Result2}, {Result3}", result1, result2, result3);
                return;
            }

            var index = AudioDevices.FindIndex(d => d.DeviceId == deviceId);

            if (index != -1)
            {
                AudioDevices.First().IsSelected = false;
                AudioDevices[index].IsSelected = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting default device");
            return;
        }
        finally
        {
            if (policyConfigObject is not null)
            {
                Marshal.ReleaseComObject(policyConfigObject);
            }
        }
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        logger.LogInformation("Device state changed: {DeviceId} - {NewState}", deviceId, newState);
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        try
        {
            var device = enumerator.GetDevice(pwstrDeviceId);
            if (device is null || device.State is not DeviceState.Active) return;

            var audioDevice = new AudioDeviceInfo
            {
                InfoType = AudioInfoType.New,
                DeviceId = pwstrDeviceId,
                DeviceName = device.FriendlyName,
                Volume = device.AudioEndpointVolume.MasterVolumeLevelScalar,
                IsMuted = device.AudioEndpointVolume.Mute,
                IsSelected = false
            };

            AudioDevices.Add(audioDevice);

            // Subscribe to volume change notifications
            var handler = new DeviceVolumeNotificationHandler(device.ID, device);
            handler.SetHandleAction(OnDeviceVolumeChanged);
            device.AudioEndpointVolume.OnVolumeNotification += handler.Handle;

            if (deviceHandlers.TryGetValue(device.ID, out var existingHandler))
            {
                existingHandler.Unsubscribe();
            }
            deviceHandlers[device.ID] = handler;

            SendAudioDeviceUpdate(audioDevice);

            logger.LogInformation("Device added: {DeviceId}", pwstrDeviceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding device {DeviceId}", pwstrDeviceId);
        }
    }

    public void OnDeviceRemoved(string deviceId)
    {
        if (deviceHandlers.TryGetValue(deviceId, out var handler))
        {
            handler.Unsubscribe();
            deviceHandlers.Remove(deviceId);
        }

        var audioDevice = AudioDevices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (audioDevice is not null)
        {
            audioDevice.InfoType = AudioInfoType.Removed;
            SendAudioDeviceUpdate(audioDevice);
        }

        AudioDevices.RemoveAll(d => d.DeviceId == deviceId);
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow is not DataFlow.Render || (role is not Role.Multimedia && role is not Role.Console)) return;

        var index = AudioDevices.FindIndex(d => d.DeviceId == defaultDeviceId);

        if (index != -1)
        {
            var selectedIndex = AudioDevices.FindIndex(d => d.IsSelected == true);
            if (selectedIndex != -1)
            {
                AudioDevices[selectedIndex].IsSelected = false;
                AudioDevices[selectedIndex].InfoType = AudioInfoType.Active;
                SendAudioDeviceUpdate(AudioDevices[selectedIndex]);
            }

            AudioDevices[index].IsSelected = true;
            AudioDevices[index].InfoType = AudioInfoType.Active;
            SendAudioDeviceUpdate(AudioDevices[index]);

            logger.LogInformation("Default device changed: {DefaultDeviceId}", defaultDeviceId);
        }
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
    }
}

public class DeviceVolumeNotificationHandler(string deviceId, MMDevice device)
{
    private Action<string, AudioVolumeNotificationData>? handleAction;

    public void Handle(AudioVolumeNotificationData data)
    {
        handleAction?.Invoke(deviceId, data);
    }

    public void SetHandleAction(Action<string, AudioVolumeNotificationData> action)
    {
        handleAction = action;
    }

    public void Unsubscribe()
    {
        try
        {
            device.AudioEndpointVolume.OnVolumeNotification -= Handle;
        }
        catch
        {
            // Device may already be disposed
        }
    }
}
