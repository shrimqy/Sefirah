using Windows.ApplicationModel.Calls;
using WinRTPhoneCall = Windows.ApplicationModel.Calls.PhoneCall;

namespace Sefirah.Platforms.Windows.Calling;

public sealed partial class PhoneCall(WinRTPhoneCall call) : IPhoneCall
{
    private readonly Lock eventLock = new();
    private EventHandler? audioDeviceChanged;
    private EventHandler? statusChanged;
    private EventHandler? isMutedChanged;
    private bool disposed;

    public CallingPhoneCallAudioDevice AudioDevice => MapAudioDevice(call.AudioDevice);
    public string CallId => call.CallId;
    public CallingPhoneCallStatus Status => MapWinRtStatus(call.Status);
    public bool IsMuted => call.IsMuted;

    public event EventHandler? AudioDeviceChanged
    {
        add
        {
            lock (eventLock)
            {
                if (audioDeviceChanged is null)
                    call.AudioDeviceChanged += OnAudioDeviceChanged;
                audioDeviceChanged += value;
            }
        }
        remove
        {
            lock (eventLock)
            {
                audioDeviceChanged -= value;
                if (audioDeviceChanged is null)
                    call.AudioDeviceChanged -= OnAudioDeviceChanged;
            }
        }
    }

    public event EventHandler? StatusChanged
    {
        add
        {
            lock (eventLock)
            {
                if (statusChanged is null)
                    call.StatusChanged += OnStatusChanged;
                statusChanged += value;
            }
        }
        remove
        {
            lock (eventLock)
            {
                statusChanged -= value;
                if (statusChanged is null)
                    call.StatusChanged -= OnStatusChanged;
            }
        }
    }

    public event EventHandler? IsMutedChanged
    {
        add
        {
            lock (eventLock)
            {
                if (isMutedChanged is null)
                    call.IsMutedChanged += OnIsMutedChanged;
                isMutedChanged += value;
            }
        }
        remove
        {
            lock (eventLock)
            {
                isMutedChanged -= value;
                if (isMutedChanged is null)
                    call.IsMutedChanged -= OnIsMutedChanged;
            }
        }
    }

    public static IPhoneCall? FromCallId(string callId)
    {
        if (string.IsNullOrEmpty(callId)) return null;
        var c = WinRTPhoneCall.GetFromId(callId);
        return c is null ? null : new PhoneCall(c);
    }

    public async Task<CallingPhoneCallOperationStatus> ChangeAudioDeviceAsync(CallingPhoneCallAudioDevice device) =>
        MapOpStatus(await call.ChangeAudioDeviceAsync(UnmapAudioDevice(device)));

    public async Task<CallingPhoneCallOperationStatus> AcceptIncomingAsync() =>
        MapOpStatus(await call.AcceptIncomingAsync());

    public async Task<CallingPhoneCallOperationStatus> RejectIncomingAsync() =>
        MapOpStatus(await call.RejectIncomingAsync());

    public async Task<CallingPhoneCallOperationStatus> EndAsync() =>
        MapOpStatus(await call.EndAsync());

    public async Task<CallingPhoneCallOperationStatus> HoldAsync() =>
        MapOpStatus(await call.HoldAsync());

    public async Task<CallingPhoneCallOperationStatus> ResumeFromHoldAsync() =>
        MapOpStatus(await call.ResumeFromHoldAsync());

    public async Task<CallingPhoneCallOperationStatus> MuteAsync() =>
        MapOpStatus(await call.MuteAsync());

    public async Task<CallingPhoneCallOperationStatus> UnmuteAsync() =>
        MapOpStatus(await call.UnmuteAsync());

    public async Task<IPhoneCallInfo> GetPhoneCallInfoAsync()
    {
        var info = await call.GetPhoneCallInfoAsync();
        return new PhoneCallInfo(info);
    }

    public void Dispose()
    {
        if (disposed) return;

        lock (eventLock)
        {
            call.AudioDeviceChanged -= OnAudioDeviceChanged;
            call.StatusChanged -= OnStatusChanged;
            call.IsMutedChanged -= OnIsMutedChanged;
            audioDeviceChanged = null;
            statusChanged = null;
            isMutedChanged = null;
        }

        disposed = true;
    }

    private void OnAudioDeviceChanged(WinRTPhoneCall sender, object args) =>
        audioDeviceChanged?.Invoke(this, EventArgs.Empty);

    private void OnStatusChanged(WinRTPhoneCall sender, object args) =>
        statusChanged?.Invoke(this, EventArgs.Empty);

    private void OnIsMutedChanged(WinRTPhoneCall sender, object args) =>
        isMutedChanged?.Invoke(this, EventArgs.Empty);

    private static CallingPhoneCallOperationStatus MapOpStatus(PhoneCallOperationStatus status) =>
        status switch
        {
            PhoneCallOperationStatus.Succeeded => CallingPhoneCallOperationStatus.Succeeded,
            PhoneCallOperationStatus.InvalidCallState => CallingPhoneCallOperationStatus.InvalidCallState,
            PhoneCallOperationStatus.TimedOut => CallingPhoneCallOperationStatus.TimedOut,
            PhoneCallOperationStatus.ConnectionLost => CallingPhoneCallOperationStatus.ConnectionLost,
            PhoneCallOperationStatus.OtherFailure => CallingPhoneCallOperationStatus.OtherFailure,
            _ => CallingPhoneCallOperationStatus.OtherFailure,
        };

    private static CallingPhoneCallAudioDevice MapAudioDevice(PhoneCallAudioDevice d) =>
        d switch
        {
            PhoneCallAudioDevice.LocalDevice => CallingPhoneCallAudioDevice.LocalDevice,
            PhoneCallAudioDevice.RemoteDevice => CallingPhoneCallAudioDevice.RemoteDevice,
            PhoneCallAudioDevice.Unknown => CallingPhoneCallAudioDevice.Unknown,
            _ => CallingPhoneCallAudioDevice.Unknown,
        };

    private static PhoneCallAudioDevice UnmapAudioDevice(CallingPhoneCallAudioDevice d) =>
        d switch
        {
            CallingPhoneCallAudioDevice.LocalDevice => PhoneCallAudioDevice.LocalDevice,
            CallingPhoneCallAudioDevice.RemoteDevice => PhoneCallAudioDevice.RemoteDevice,
            CallingPhoneCallAudioDevice.Unknown => PhoneCallAudioDevice.Unknown,
            _ => PhoneCallAudioDevice.Unknown,
        };

    internal static CallingPhoneCallStatus MapWinRtStatus(PhoneCallStatus s) =>
        s switch
        {
            PhoneCallStatus.Incoming => CallingPhoneCallStatus.Incoming,
            PhoneCallStatus.Dialing => CallingPhoneCallStatus.Dialing,
            PhoneCallStatus.Talking => CallingPhoneCallStatus.Talking,
            PhoneCallStatus.Held => CallingPhoneCallStatus.Held,
            PhoneCallStatus.Ended => CallingPhoneCallStatus.Ended,
            PhoneCallStatus.Lost => CallingPhoneCallStatus.Lost,
            _ => CallingPhoneCallStatus.Unknown,
        };
}
