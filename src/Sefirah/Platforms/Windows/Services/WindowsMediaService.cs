using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Platforms.Windows.Interop;
using Windows.Media;
using Windows.Media.Control;

namespace Sefirah.Platforms.Windows.Services;

public class WindowsMediaService(
    ILogger logger,
    ISessionManager sessionManager,
    IDeviceManager deviceManager) : IMediaService, IMMNotificationClient
{
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly ConcurrentDictionary<string, GlobalSystemMediaTransportControlsSession> activeSessions = [];
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private string? defaultDeviceId;
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
                logger.Error($"Failed to initialize GlobalSystemMediaTransportControlsSessionManager");
                return;
            }

            InitializeAudioDevices();
            UpdateActiveSessions();

            enumerator.RegisterEndpointNotificationCallback(this);

            manager.SessionsChanged += SessionsChanged;
            sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;

            logger.Info("MediaService initialized successfully");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize MediaService", ex);
        }
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (!device.IsConnected) return;

        if (device.DeviceSettings.MediaSessionSend)
        {
            foreach (var session in activeSessions.Values)
            {
                UpdatePlaybackDataAsync(session);
            }
        }

        if (device.DeviceSettings.AudioSync)
        {
            foreach (var deviceId in deviceHandlers.Keys.ToList())
            {
                try
                {
                    var audioDevice = enumerator.GetDevice(deviceId);
                    if (audioDevice is null || audioDevice.State is not DeviceState.Active) continue;
                    var info = GetAudioDeviceInfo(audioDevice, deviceId == defaultDeviceId, AudioInfoType.New);
                    device.SendMessage(info);
                }
                catch
                {
                    // Skip if device no longer valid
                }
            }
        }
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
            logger.Error($"Error updating active sessions", ex);
        }
    }

    private void UpdateSessionsList(IReadOnlyList<GlobalSystemMediaTransportControlsSession> activeSessions)
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

    private void RemoveSession(string sessionId)
    {
        if(activeSessions.TryRemove(sessionId, out var session)) 
        {
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
            logger.Error($"Error processing timeline properties for {sender.SourceAppUserModelId}", ex);
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

    private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args)
    {
        UpdatePlaybackDataAsync(session);
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
                {
                    device.SendMessage(playbackSession);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error sending playback data", ex);
        }
    }

    private static AudioDeviceInfo GetAudioDeviceInfo(MMDevice device, bool isSelected, AudioInfoType infoType)
    {
        return new AudioDeviceInfo
        {
            InfoType = infoType,
            DeviceId = device.ID,
            DeviceName = device.FriendlyName,
            Volume = device.AudioEndpointVolume.MasterVolumeLevelScalar,
            IsMuted = device.AudioEndpointVolume.Mute,
            IsSelected = isSelected
        };
    }

    private void InitializeAudioDevices()
    {
        try
        {
            defaultDeviceId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;

            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                var handler = new DeviceVolumeNotificationHandler(device.ID, device.FriendlyName, device);
                handler.SetHandleAction(OnDeviceVolumeChanged);
                device.AudioEndpointVolume.OnVolumeNotification += handler.Handle;
                deviceHandlers[device.ID] = handler;
            }
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to enumerate audio devices", ex);
        }
    }

    private void OnDeviceVolumeChanged(string deviceId, string friendlyName, AudioVolumeNotificationData data)
    {
        if (!deviceHandlers.ContainsKey(deviceId)) return;

        var audioInfo = new AudioDeviceInfo
        {
            InfoType = AudioInfoType.Active,
            DeviceId = deviceId,
            DeviceName = friendlyName,
            Volume = data.MasterVolume,
            IsMuted = data.Muted,
            IsSelected = deviceId == defaultDeviceId
        };

        SendAudioDeviceUpdate(audioInfo);
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
            logger.Error($"Error sending audio device update", ex);
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
                logger.Warn($"Device {deviceId} not functioning when muting");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error muting device {deviceId}", ex);
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
                logger.Warn($"Device {deviceId} not functioning when setting volume");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error setting volume to {volume} for device {deviceId}", ex);
        }
    }


    public void SetDefaultAudioDevice(string deviceId)
    {
        IPolicyConfig? policyConfig = null;
        try
        {
            policyConfig = new IPolicyConfig();
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications));

            if (deviceHandlers.ContainsKey(deviceId))
                defaultDeviceId = deviceId;
        }
        catch (Exception ex)
        {
            logger.Error("Error setting default audio device", ex);
        }
        finally
        {
            if (policyConfig is not null)
                Marshal.ReleaseComObject(policyConfig);
        }
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        //logger.Info($"Device state changed: {deviceId} - {newState}");
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        try
        {
            var device = enumerator.GetDevice(pwstrDeviceId);
            if (device is null || device.State is not DeviceState.Active) return;

            var handler = new DeviceVolumeNotificationHandler(device.ID, device.FriendlyName, device);
            handler.SetHandleAction(OnDeviceVolumeChanged);
            device.AudioEndpointVolume.OnVolumeNotification += handler.Handle;

            if (deviceHandlers.TryGetValue(device.ID, out var existingHandler))
                existingHandler.Unsubscribe();
            deviceHandlers[device.ID] = handler;

            var info = GetAudioDeviceInfo(device, false, AudioInfoType.New);
            SendAudioDeviceUpdate(info);

            logger.Info($"Device added: {pwstrDeviceId}");
        }
        catch (Exception ex)
        {
            logger.Error($"Error adding device {pwstrDeviceId}", ex);
        }
    }

    public void OnDeviceRemoved(string deviceId)
    {
        if (deviceHandlers.TryGetValue(deviceId, out var handler))
        {
            handler.Unsubscribe();
            deviceHandlers.Remove(deviceId);
        }

        SendAudioDeviceUpdate(new AudioDeviceInfo
        {
            InfoType = AudioInfoType.Removed,
            DeviceId = deviceId,
        });
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string newDefaultDeviceId)
    {
        if (flow is not DataFlow.Render || (role is not Role.Multimedia && role is not Role.Console)) return;

        var previousDefaultId = defaultDeviceId;
        defaultDeviceId = newDefaultDeviceId;

        if (previousDefaultId is not null && deviceHandlers.ContainsKey(previousDefaultId))
        {
            try
            {
                var prevDevice = enumerator.GetDevice(previousDefaultId);
                if (prevDevice is not null && prevDevice.State is DeviceState.Active)
                {
                    var prevInfo = GetAudioDeviceInfo(prevDevice, false, AudioInfoType.Active);
                    SendAudioDeviceUpdate(prevInfo);
                }
            }
            catch { }
        }

        if (deviceHandlers.ContainsKey(newDefaultDeviceId))
        {
            try
            {
                var newDevice = enumerator.GetDevice(newDefaultDeviceId);
                if (newDevice is not null && newDevice.State == DeviceState.Active)
                {
                    var newInfo = GetAudioDeviceInfo(newDevice, true, AudioInfoType.Active);
                    SendAudioDeviceUpdate(newInfo);
                }
            }
            catch { }
        }

        logger.Info($"Default device changed: {newDefaultDeviceId}");
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
    }
}

public class DeviceVolumeNotificationHandler(string deviceId, string friendlyName, MMDevice device)
{
    private Action<string, string, AudioVolumeNotificationData>? handleAction;

    public void Handle(AudioVolumeNotificationData data)
    {
        handleAction?.Invoke(deviceId, friendlyName, data);
    }

    public void SetHandleAction(Action<string, string, AudioVolumeNotificationData> action)
    {
        handleAction = action;
    }

    public void Unsubscribe()
    {
        try
        {
            device.AudioEndpointVolume.OnVolumeNotification -= Handle;
        }
        catch { }
    }
}
