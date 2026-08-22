using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Sefirah.Data.Models;
using Sefirah.Platforms.Windows.Interop;

namespace Sefirah.Platforms.Windows.Features;

public class AudioFeature(
    ILogger logger,
    ISessionManager sessionManager,
    IDeviceManager deviceManager) : IAudioFeature, IMMNotificationClient
{
    private string? defaultDeviceId;
    private readonly MMDeviceEnumerator enumerator = new();
    private readonly Dictionary<string, DeviceVolumeNotificationHandler> deviceHandlers = [];

    /// <inheritdoc/>
    public Task InitializeAsync()
    {
        try
        {
            InitializeAudioDevices();
            enumerator.RegisterEndpointNotificationCallback(this);
            sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            logger.Info("Audio devices initialized");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize audio devices", ex);
        }

        return Task.CompletedTask;
    }

    public Task HandleAudioActionAsync(AudioAction action)
    {
        try
        {
            switch (action.ActionType)
            {
                case AudioActionType.DefaultDevice:
                    SetDefaultAudioDevice(action.Source);
                    break;
                case AudioActionType.VolumeUpdate:
                    if (action.Value.HasValue)
                        SetVolume(action.Source, Convert.ToSingle(action.Value.Value));
                    break;
                case AudioActionType.ToggleMute:
                    ToggleMute(action.Source);
                    break;
                default:
                    logger.Warn($"Unhandled audio action: {action.ActionType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error executing audio action {action.ActionType}", ex);
        }

        return Task.CompletedTask;
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (!device.IsConnected || !device.DeviceSettings.AudioSync) return;

        foreach (var deviceId in deviceHandlers.Keys.ToList())
        {
            try
            {
                var audioDevice = enumerator.GetDevice(deviceId);
                if (audioDevice is null || audioDevice.State is not DeviceState.Active) continue;
                device.SendMessage(GetAudioDeviceInfo(audioDevice, deviceId == defaultDeviceId, AudioInfoType.New));
            }
            catch
            {
                // Skip if device no longer valid
            }
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

        SendAudioDeviceUpdate(new AudioDeviceInfo
        {
            InfoType = AudioInfoType.Active,
            DeviceId = deviceId,
            DeviceName = friendlyName,
            Volume = data.MasterVolume,
            IsMuted = data.Muted,
            IsSelected = deviceId == defaultDeviceId
        });
    }

    private void SendAudioDeviceUpdate(AudioDeviceInfo audioDevice)
    {
        try
        {
            foreach (var device in deviceManager.PairedDevices)
            {
                if (device.IsConnected && device.DeviceSettings.AudioSync)
                    device.SendMessage(audioDevice);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error sending audio device update", ex);
        }
    }

    private void ToggleMute(string deviceId)
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

    private void SetVolume(string deviceId, float volume)
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

    private void SetDefaultAudioDevice(string deviceId)
    {
        IPolicyConfig? policyConfig = null;
        try
        {
            policyConfig = new IPolicyConfig();
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications));
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

            SendAudioDeviceUpdate(GetAudioDeviceInfo(device, false, AudioInfoType.New));
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
        if (newDefaultDeviceId == defaultDeviceId) return;

        var previousDefaultId = defaultDeviceId;
        defaultDeviceId = newDefaultDeviceId;

        if (previousDefaultId is not null && deviceHandlers.ContainsKey(previousDefaultId))
        {
            try
            {
                var prevDevice = enumerator.GetDevice(previousDefaultId);
                if (prevDevice is not null && prevDevice.State is DeviceState.Active)
                    SendAudioDeviceUpdate(GetAudioDeviceInfo(prevDevice, false, AudioInfoType.Active));
            }
            catch { }
        }

        if (deviceHandlers.ContainsKey(newDefaultDeviceId))
        {
            try
            {
                var newDevice = enumerator.GetDevice(newDefaultDeviceId);
                if (newDevice is not null && newDevice.State == DeviceState.Active)
                    SendAudioDeviceUpdate(GetAudioDeviceInfo(newDevice, true, AudioInfoType.Active));
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
