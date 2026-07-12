using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sefirah.Data.Models;

namespace Sefirah.Platforms.Desktop.Services;

internal sealed class PulseAudioClient : IAsyncDisposable
{
    private const float MaxVolumeScalar = 1.5f;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<PulseAudioClient> logger;
    private readonly Lock sync = new();
    private readonly LibPulseInterop.PaContextStateCallback contextStateCallback;
    private readonly LibPulseInterop.PaServerInfoCallback serverInfoCallback;
    private readonly LibPulseInterop.PaSinkInfoCallback sinkInfoListCallback;
    private readonly LibPulseInterop.PaSinkInfoCallback resolveSinkCallback;
    private readonly LibPulseInterop.PaSubscribeCallback subscribeCallback;

    private IntPtr mainloop;
    private IntPtr context;
    private string defaultSinkName = string.Empty;
    private HashSet<string> knownSinkNames = new(StringComparer.Ordinal);
    private bool isAvailable;

    public PulseAudioClient(ILogger<PulseAudioClient> logger)
    {
        this.logger = logger;
        contextStateCallback = OnContextStateChanged;
        serverInfoCallback = OnServerInfo;
        sinkInfoListCallback = OnSinkInfoList;
        resolveSinkCallback = OnResolveSinkInfo;
        subscribeCallback = OnSubscribeEvent;
    }

    public event Action<IReadOnlyList<PulseAudioSink>>? SinksChanged;
    public event Action<string>? SinkRemoved;

    public bool IsAvailable => isAvailable;

    public void Initialize()
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            LibPulseInterop.EnsureResolver();

            if (!Open())
            {
                logger.Warn("Failed to connect to PulseAudio/PipeWire; system audio sync is disabled. Ensure pipewire-pulse or pulseaudio is running.");
                return;
            }

            isAvailable = true;
            NotifySinkListChanged();
            logger.Info("PulseAudio connected via libpulse");
        }
        catch (DllNotFoundException ex)
        {
            logger.Warn("libpulse was not found; system audio sync is disabled. Install pipewire-pulse on Linux.", ex);
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to initialize PulseAudio client; system audio sync is disabled", ex);
        }
    }

    public IReadOnlyList<PulseAudioSink> GetCurrentSinks()
    {
        if (!IsAvailableForOperations())
            return [];

        try
        {
            return ListSinksCore();
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to list PulseAudio sinks", ex);
            return [];
        }
    }

    public void SetVolume(string sinkName, float volumeScalar)
    {
        if (!IsAvailableForOperations())
            return;

        var volume = NormalizeVolumeScalar(volumeScalar);
        QueueSinkAction( () => SetSinkVolumeCore(sinkName, volume), $"Failed to set volume for sink {sinkName}");
    }

    public void ToggleMute(string sinkName) =>
        QueueSinkAction(() => SetSinkMuteCore(sinkName, muted: -1), $"Failed to toggle mute for sink {sinkName}");

    public void SetDefaultSink(string sinkName) =>
        QueueSinkAction(() => SetDefaultSinkCore(sinkName), $"Failed to set default sink {sinkName}");

    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            isAvailable = false;
            if (mainloop != IntPtr.Zero)
                CleanupConnection();
        }

        return ValueTask.CompletedTask;
    }

    private void QueueSinkAction(Func<bool> action, string failureMessage)
    {
        if (!IsAvailableForOperations())
            return;

        _ = Task.Run(() =>
        {
            if (!action())
                logger.Warn(failureMessage);
        });
    }

    private bool IsAvailableForOperations()
    {
        lock (sync)
            return isAvailable;
    }

    private bool Open()
    {
        CleanupConnection();

        mainloop = LibPulseInterop.pa_threaded_mainloop_new();
        if (mainloop == IntPtr.Zero)
            return false;

        if (LibPulseInterop.pa_threaded_mainloop_start(mainloop) < 0)
        {
            CleanupConnection();
            return false;
        }

        using var connectTimeout = new CancellationTokenSource(ConnectionTimeout);
        connectTimeout.Token.Register(SignalMainloopSafe);

        var connected = false;
        RunOnMainloop(() =>
        {
            var api = LibPulseInterop.pa_threaded_mainloop_get_api(mainloop);
            context = LibPulseInterop.pa_context_new(api, "Sefirah");
            if (context == IntPtr.Zero)
                return;

            LibPulseInterop.pa_context_set_state_callback(context, contextStateCallback, IntPtr.Zero);

            if (LibPulseInterop.pa_context_connect(context, IntPtr.Zero, 0, IntPtr.Zero) < 0)
                return;

            if (!WaitForContextReady(connectTimeout.Token))
                return;

            RefreshDefaultSinkName();

            LibPulseInterop.pa_context_set_subscribe_callback(context, subscribeCallback, IntPtr.Zero);
            var subscribeOperation = LibPulseInterop.pa_context_subscribe(
                context,
                LibPulseInterop.SubscriptionMaskSink | LibPulseInterop.SubscriptionMaskServer,
                IntPtr.Zero,
                IntPtr.Zero);

            if (subscribeOperation == IntPtr.Zero)
                return;

            WaitForOperation(subscribeOperation, connectTimeout.Token);
            connected = true;
        });

        if (!connected)
            CleanupConnection();

        return connected;
    }

    private void CleanupConnection()
    {
        if (mainloop == IntPtr.Zero)
            return;

        try
        {
            RunOnMainloop(() =>
            {
                if (context == IntPtr.Zero)
                    return;

                LibPulseInterop.pa_context_disconnect(context);
                LibPulseInterop.pa_context_unref(context);
                context = IntPtr.Zero;
            });
        }
        catch (Exception ex)
        {
            logger.Warn("Error while cleaning up PulseAudio connection", ex);
        }

        try
        {
            LibPulseInterop.pa_threaded_mainloop_stop(mainloop);
            LibPulseInterop.pa_threaded_mainloop_free(mainloop);
        }
        catch (Exception ex)
        {
            logger.Warn("Error while stopping PulseAudio mainloop", ex);
        }

        mainloop = IntPtr.Zero;
        defaultSinkName = string.Empty;
    }

    private void RunOnMainloop(Action action)
    {
        LockMainloop();
        try
        {
            action();
        }
        finally
        {
            UnlockMainloop();
        }
    }

    private T RunOnMainloop<T>(Func<T> action)
    {
        LockMainloop();
        try
        {
            return action();
        }
        finally
        {
            UnlockMainloop();
        }
    }

    private void LockMainloop() => LibPulseInterop.pa_threaded_mainloop_lock(mainloop);

    private void UnlockMainloop() => LibPulseInterop.pa_threaded_mainloop_unlock(mainloop);

    private void SignalMainloopSafe()
    {
        if (mainloop != IntPtr.Zero)
            LibPulseInterop.pa_threaded_mainloop_signal(mainloop, 0);
    }

    private bool WaitForContextReady(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var state = LibPulseInterop.pa_context_get_state(context);
            if (state == LibPulseInterop.ContextReady)
                return true;

            if (state is LibPulseInterop.ContextFailed or LibPulseInterop.ContextTerminated)
                return false;

            LibPulseInterop.pa_threaded_mainloop_wait(mainloop);
        }

        return false;
    }

    private void WaitForOperation(IntPtr operation, CancellationToken cancellationToken = default)
    {
        if (operation == IntPtr.Zero)
            return;

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   LibPulseInterop.pa_operation_get_state(operation) == LibPulseInterop.OperationRunning)
            {
                LibPulseInterop.pa_threaded_mainloop_wait(mainloop);
            }
        }
        finally
        {
            LibPulseInterop.pa_operation_unref(operation);
        }
    }

    private void RefreshDefaultSinkName()
    {
        var operation = LibPulseInterop.pa_context_get_server_info(context, serverInfoCallback, IntPtr.Zero);
        if (operation != IntPtr.Zero)
            WaitForOperation(operation);
    }

    private List<PulseAudioSink> ListSinksCore() =>
        RunOnMainloop(ListSinksUnlocked);

    private List<PulseAudioSink> ListSinksUnlocked()
    {
        var state = new SinkListState();
        var handle = GCHandle.Alloc(state);

        try
        {
            var operation = LibPulseInterop.pa_context_get_sink_info_list(context, sinkInfoListCallback, GCHandle.ToIntPtr(handle));
            if (operation == IntPtr.Zero)
                throw new InvalidOperationException("pa_context_get_sink_info_list failed");

            WaitForOperation(operation);
            RefreshDefaultSinkName();

            return state.Sinks.Select(sink => sink with { IsDefault = string.Equals(sink.Name, defaultSinkName, StringComparison.Ordinal) }).ToList();
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }

    private bool SetSinkVolumeCore(string sinkName, float volume) =>
        RunOnMainloop(() => SetSinkVolumeUnlocked(sinkName, volume));

    private bool SetSinkVolumeUnlocked(string sinkName, float volumeScalar)
    {
        if (!TryResolveSinkUnlocked(sinkName, out var sink) || sink is null)
        {
            logger.Warn($"PulseAudio sink not found for volume update: {sinkName}");
            return false;
        }

        unsafe
        {
            var channelVolume = sink.Volume;
            if (channelVolume.Channels == 0)
                return false;

            var volumePtr = (IntPtr)Unsafe.AsPointer(ref channelVolume);
            // Same scale as ReadVolumeScalar (fraction of PA_VOLUME_NORM).
            var target = (uint)Math.Round(volumeScalar * LibPulseInterop.VolumeNorm);
            LibPulseInterop.pa_cvolume_set(volumePtr, channelVolume.Channels, target);

            var setOperation = LibPulseInterop.pa_context_set_sink_volume_by_index(
                context,
                sink.Index,
                volumePtr,
                IntPtr.Zero,
                IntPtr.Zero);

            if (setOperation == IntPtr.Zero)
                return false;

            WaitForOperation(setOperation);
        }

        return true;
    }

    private bool SetSinkMuteCore(string sinkName, int muted) =>
        RunOnMainloop(() => SetSinkMuteUnlocked(sinkName, muted));

    private bool SetSinkMuteUnlocked(string sinkName, int muted)
    {
        if (!TryResolveSinkUnlocked(sinkName, out var sink) || sink is null)
            return false;

        var mute = muted < 0
            ? sink.CurrentMute == 0 ? 1 : 0
            : muted != 0 ? 1 : 0;

        var setOperation = LibPulseInterop.pa_context_set_sink_mute_by_index(
            context,
            sink.Index,
            mute,
            IntPtr.Zero,
            IntPtr.Zero);

        if (setOperation == IntPtr.Zero)
            return false;

        WaitForOperation(setOperation);
        return true;
    }

    private bool TryResolveSinkUnlocked(string sinkName, out ResolvedSink? sink)
    {
        if (TryResolveSinkByName(sinkName, out sink))
            return true;

        foreach (var candidate in ListSinksUnlocked())
        {
            if (!string.Equals(candidate.Name, sinkName, StringComparison.Ordinal) &&
                !string.Equals(candidate.Description, sinkName, StringComparison.Ordinal))
            {
                continue;
            }

            return TryResolveSinkByName(candidate.Name, out sink);
        }

        sink = null;
        return false;
    }

    private bool TryResolveSinkByName(string sinkName, out ResolvedSink? sink)
    {
        var state = new ResolvedSinkState();
        var handle = GCHandle.Alloc(state);

        try
        {
            var operation = LibPulseInterop.pa_context_get_sink_info_by_name(
                context,
                sinkName,
                resolveSinkCallback,
                GCHandle.ToIntPtr(handle));

            if (operation == IntPtr.Zero)
            {
                sink = null;
                return false;
            }

            WaitForOperation(operation);
            sink = state.Sink;
            return state.Found;
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }

    private bool SetDefaultSinkCore(string sinkName) =>
        RunOnMainloop(() =>
        {
            var operation = LibPulseInterop.pa_context_set_default_sink(context, sinkName, IntPtr.Zero, IntPtr.Zero);
            if (operation == IntPtr.Zero)
                return false;

            WaitForOperation(operation);
            defaultSinkName = sinkName;
            return true;
        });

    private void OnContextStateChanged(IntPtr contextPtr, IntPtr userdata)
    {
        _ = contextPtr;
        _ = userdata;
        SignalMainloopSafe();
    }

    private void OnServerInfo(IntPtr contextPtr, IntPtr info, IntPtr userdata)
    {
        _ = contextPtr;
        _ = userdata;

        if (info != IntPtr.Zero && LibPulseStructs.TryReadServerInfo(info, out var serverInfo))
            defaultSinkName = LibPulseInterop.ReadUtf8String(serverInfo.DefaultSinkName) ?? string.Empty;

        SignalMainloopSafe();
    }

    private void OnSinkInfoList(IntPtr contextPtr, IntPtr info, int endOfList, IntPtr userdata)
    {
        _ = contextPtr;

        if (TryCompleteSinkInfoCallback(endOfList))
            return;

        if (info == IntPtr.Zero || userdata == IntPtr.Zero)
            return;

        var state = (SinkListState)GCHandle.FromIntPtr(userdata).Target!;
        state.Sinks.Add(CreateSink(info));
    }

    private void OnResolveSinkInfo(IntPtr contextPtr, IntPtr info, int endOfList, IntPtr userdata)
    {
        _ = contextPtr;

        if (TryCompleteSinkInfoCallback(endOfList))
            return;

        if (info == IntPtr.Zero || userdata == IntPtr.Zero)
            return;

        if (!LibPulseStructs.TryReadSinkInfo(info, out var sinkInfo))
            return;

        var state = (ResolvedSinkState)GCHandle.FromIntPtr(userdata).Target!;
        state.Found = true;
        state.Sink.Index = sinkInfo.Index;
        state.Sink.CurrentMute = sinkInfo.Mute;
        state.Sink.Volume = sinkInfo.Volume;
    }

    private void OnSubscribeEvent(IntPtr contextPtr, uint eventType, uint index, IntPtr userdata)
    {
        _ = contextPtr;
        _ = index;
        _ = userdata;

        var facility = eventType & LibPulseInterop.SubscriptionEventFacilityMask;
        if (facility is not LibPulseInterop.SubscriptionEventSink and not LibPulseInterop.SubscriptionEventServer)
            return;

        Task.Run(NotifySinkListChangedSafe);
    }

    private static float NormalizeVolumeScalar(float volumeScalar)
    {
        var volume = volumeScalar;
        if (volume > 1f)
            volume /= 100f;

        return Math.Clamp(volume, 0f, MaxVolumeScalar);
    }

    private bool TryCompleteSinkInfoCallback(int endOfList)
    {
        if (endOfList <= 0)
            return false;

        SignalMainloopSafe();
        return true;
    }

    private void NotifySinkListChangedSafe()
    {
        try
        {
            NotifySinkListChanged();
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to refresh PulseAudio sinks after native event", ex);
        }
    }

    private void NotifySinkListChanged()
    {
        var sinks = GetCurrentSinks();
        var currentNames = sinks.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

        HashSet<string> previousNames;
        lock (sync)
        {
            previousNames = knownSinkNames;
            knownSinkNames = currentNames;
        }

        foreach (var removedName in previousNames.Except(currentNames))
            SinkRemoved?.Invoke(removedName);

        SinksChanged?.Invoke(sinks);
    }

    private PulseAudioSink CreateSink(IntPtr info)
    {
        if (!LibPulseStructs.TryReadSinkInfo(info, out var sinkInfo))
            return new PulseAudioSink(string.Empty, string.Empty, 0f, false, false);

        var name = LibPulseInterop.ReadUtf8String(sinkInfo.Name) ?? string.Empty;
        var description = LibPulseInterop.ReadUtf8String(sinkInfo.Description);
        if (string.IsNullOrEmpty(description))
            description = name;

        return new PulseAudioSink(
            name,
            description,
            LibPulseInterop.ReadVolumeScalar(sinkInfo.Volume),
            sinkInfo.Mute != 0,
            string.Equals(name, defaultSinkName, StringComparison.Ordinal));
    }

    private sealed class ResolvedSink
    {
        public uint Index;
        public int CurrentMute;
        public PaCVolume Volume;
    }

    private sealed class ResolvedSinkState
    {
        public bool Found;
        public ResolvedSink Sink { get; } = new();
    }

    private sealed class SinkListState
    {
        public List<PulseAudioSink> Sinks { get; } = [];
    }
}

internal sealed record PulseAudioSink(
    string Name,
    string Description,
    float Volume,
    bool IsMuted,
    bool IsDefault)
{
    internal AudioDeviceInfo ToAudioDeviceInfo(AudioInfoType infoType) => new()
    {
        InfoType = infoType,
        DeviceId = Name,
        DeviceName = Description,
        Volume = Volume,
        IsMuted = IsMuted,
        IsSelected = IsDefault
    };
}
