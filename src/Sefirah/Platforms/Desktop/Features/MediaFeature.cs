using Sefirah.Data.Models;
using Sefirah.Platforms.Desktop.Services;

namespace Sefirah.Platforms.Desktop.Features;

public sealed class MediaFeature(
    ILogger<MediaFeature> logger,
    ILoggerFactory loggerFactory,
    ISessionManager sessionManager,
    IDeviceManager deviceManager) : IMediaFeature, IAsyncDisposable
{
    private readonly PulseAudioClient pulseAudioClient = new(loggerFactory.CreateLogger<PulseAudioClient>());
    private readonly Dictionary<string, PulseAudioSink> knownSinks = new(StringComparer.Ordinal);
    private readonly Lock knownSinksSyncRoot = new();

    public async Task InitializeAsync()
    {
        try
        {
            pulseAudioClient.Initialize();
            if (!pulseAudioClient.IsAvailable)
                return;

            logger.Info("PulseAudio system volume sync initialized");
            SyncAudioDevicesToConnectedPeers();

            pulseAudioClient.SinksChanged += OnPulseSinksChanged;
            pulseAudioClient.SinkRemoved += OnPulseSinkRemoved;
            sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to initialize PulseAudio system volume sync", ex);
        }
    }

    public Task HandleMediaActionAsync(MediaAction mediaAction)
    {
        switch (mediaAction.ActionType)
        {
            case MediaActionType.DefaultDevice:
                pulseAudioClient.SetDefaultSink(mediaAction.Source);
                break;
            case MediaActionType.VolumeUpdate when mediaAction.Value.HasValue:
                pulseAudioClient.SetVolume(
                    mediaAction.Source,
                    Convert.ToSingle(mediaAction.Value.Value));
                break;
            case MediaActionType.ToggleMute:
                pulseAudioClient.ToggleMute(mediaAction.Source);
                break;
        }

        return Task.CompletedTask;
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (!device.IsConnected || !device.DeviceSettings.AudioSync)
            return;

        if (!pulseAudioClient.IsAvailable)
        {
            logger.Warn("Audio sync is enabled for this device but PulseAudio is unavailable on this machine.");
            return;
        }

        SyncAudioDevicesToDevice(device);
    }

    private void OnPulseSinksChanged(IReadOnlyList<PulseAudioSink> sinks)
    {
        foreach (var sink in sinks)
        {
            bool isNew;
            PulseAudioSink? previous;
            lock (knownSinksSyncRoot)
            {
                isNew = !knownSinks.TryGetValue(sink.Name, out previous);
                knownSinks[sink.Name] = sink;
            }

            if (isNew)
                SendAudioDeviceUpdate(sink.ToAudioDeviceInfo(AudioInfoType.New));
            else if (previous is not null && HasSinkStateChanged(previous, sink))
                SendAudioDeviceUpdate(sink.ToAudioDeviceInfo(AudioInfoType.Active));
        }
    }

    private void OnPulseSinkRemoved(string sinkName)
    {
        lock (knownSinksSyncRoot)
            knownSinks.Remove(sinkName);

        SendAudioDeviceUpdate(new AudioDeviceInfo
        {
            InfoType = AudioInfoType.Removed,
            DeviceId = sinkName
        });
    }

    private void SyncAudioDevicesToConnectedPeers()
    {
        foreach (var device in deviceManager.PairedDevices)
            SyncAudioDevicesToDevice(device);
    }

    private void SyncAudioDevicesToDevice(PairedDevice device)
    {
        if (!device.IsConnected || !device.DeviceSettings.AudioSync || !pulseAudioClient.IsAvailable)
            return;

        var sinks = pulseAudioClient.GetCurrentSinks();
        logger.Info($"Syncing {sinks.Count} audio device(s) to {device.Name}");
        foreach (var sink in sinks)
        {
            device.SendMessage(sink.ToAudioDeviceInfo(AudioInfoType.New));
            lock (knownSinksSyncRoot)
                knownSinks[sink.Name] = sink;
        }
    }

    private static bool HasSinkStateChanged(PulseAudioSink previous, PulseAudioSink current) =>
        Math.Abs(previous.Volume - current.Volume) > 0.005f ||
        previous.IsMuted != current.IsMuted ||
        previous.IsDefault != current.IsDefault;

    private void SendAudioDeviceUpdate(AudioDeviceInfo audioDevice)
    {
        try
        {
            foreach (var device in deviceManager.PairedDevices)
            {
                if (device.IsConnected && device.DeviceSettings.AudioSync && pulseAudioClient.IsAvailable)
                    device.SendMessage(audioDevice);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error sending audio device update", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        sessionManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
        pulseAudioClient.SinksChanged -= OnPulseSinksChanged;
        pulseAudioClient.SinkRemoved -= OnPulseSinkRemoved;
        await pulseAudioClient.DisposeAsync().ConfigureAwait(false);
    }
}
