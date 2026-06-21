using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.Models;

namespace Sefirah.ViewModels;

public sealed partial class CallSessionViewModel : BaseViewModel, IDisposable
{
    public IPhoneCall PhoneCall;

    public string CallId => PhoneCall.CallId;

    public event EventHandler? SessionEnded;

    private readonly DispatcherQueueTimer durationTimer;

    private DateTimeOffset? callStartedUtc;

    private bool disposed;

    [ObservableProperty]
    public partial string CallerHeadline { get; set; }

    [ObservableProperty]
    public partial string CallerNumber { get; set; }

    [ObservableProperty]
    public partial BitmapImage? CallerAvatar { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string AudioRouteButtonText { get; set; }

    [ObservableProperty]
    public partial bool CanToggleAudioRoute { get; set; }

    [ObservableProperty]
    public partial bool CanToggleHold { get; set; }

    [ObservableProperty]
    public partial bool IsHoldActive { get; set; }

    [ObservableProperty]
    public partial bool CanToggleMute { get; set; }

    [ObservableProperty]
    public partial bool IsMuteActive { get; set; }

    public CallSessionViewModel(IPhoneCall call, CallerContact callerContact) : base()
    {
        PhoneCall = call;
        CallerHeadline = callerContact.DisplayName;
        CallerNumber = callerContact.Address;
        CallerAvatar = callerContact.Avatar;
        StatusText = string.Empty;
        AudioRouteButtonText = string.Empty;
        IsHoldActive = false;
        IsMuteActive = false;

        durationTimer = dispatcher.CreateTimer();
        durationTimer.Interval = TimeSpan.FromSeconds(1);
        durationTimer.IsRepeating = true;
        durationTimer.Tick += OnCallDurationTick;

        call.StatusChanged += OnCallStatusChanged;
        call.AudioDeviceChanged += OnCallAudioDeviceChanged;
        call.IsMutedChanged += OnCallIsMutedChanged;
        RefreshStatus();
        RefreshAudioDevice();
        RefreshMuted();
    }

    private void OnCallStatusChanged(object? sender, EventArgs e) => dispatcher.TryEnqueue(RefreshStatus);

    private void OnCallAudioDeviceChanged(object? sender, EventArgs e) => dispatcher.TryEnqueue(RefreshAudioDevice);

    private void OnCallIsMutedChanged(object? sender, EventArgs e) => dispatcher.TryEnqueue(RefreshMuted);

    private void OnCallDurationTick(DispatcherQueueTimer sender, object args)
    {
        if (disposed || PhoneCall.Status is not CallingPhoneCallStatus.Talking || callStartedUtc is null)
        {
            return;
        }

        StatusText = FormatCallDuration(DateTimeOffset.UtcNow - callStartedUtc.Value);
    }

    private void RefreshStatus()
    {
        if (disposed)
        {
            return;
        }

        if (PhoneCall.Status is CallingPhoneCallStatus.Ended or CallingPhoneCallStatus.Lost)
        {
            StopDurationTimer();
            StatusText = string.Empty;
            SessionEnded?.Invoke(this, EventArgs.Empty);
            return;
        }

        InitializeTimer();

        CanToggleHold = PhoneCall.Status is CallingPhoneCallStatus.Talking or CallingPhoneCallStatus.Held;
        CanToggleMute = PhoneCall.Status is CallingPhoneCallStatus.Talking or CallingPhoneCallStatus.Held;
        IsHoldActive = PhoneCall.Status is CallingPhoneCallStatus.Held;
    }

    private void RefreshAudioDevice()
    {
        if (disposed)
        {
            return;
        }

        AudioRouteButtonText = PhoneCall.AudioDevice switch
        {
            CallingPhoneCallAudioDevice.RemoteDevice => "CallAudioRouteToPc".GetLocalizedResource(),
            CallingPhoneCallAudioDevice.LocalDevice => "CallAudioRouteToPhone".GetLocalizedResource(),
            _ => "CallAudioRouteToPhone".GetLocalizedResource(),
        };
        CanToggleAudioRoute = PhoneCall.AudioDevice is not CallingPhoneCallAudioDevice.Unknown;
    }

    private void RefreshMuted()
    {
        if (disposed)
        {
            return;
        }

        IsMuteActive = PhoneCall.IsMuted;
    }

    private void InitializeTimer()
    {
        if (PhoneCall.Status is CallingPhoneCallStatus.Talking)
        {
            callStartedUtc ??= DateTimeOffset.UtcNow;
            StartDurationTimer();
            StatusText = FormatCallDuration(DateTimeOffset.UtcNow - callStartedUtc.Value);
            return;
        }

        StopDurationTimer();

        StatusText = PhoneCall.Status switch
        {
            CallingPhoneCallStatus.Incoming => "CallIncoming".GetLocalizedResource(),
            CallingPhoneCallStatus.Dialing => "CallDialing".GetLocalizedResource(),
            CallingPhoneCallStatus.Held => "CallOnHold".GetLocalizedResource(),
            CallingPhoneCallStatus.Unknown => "CallConnecting".GetLocalizedResource(),
            _ => PhoneCall.Status.ToString(),
        };
    }

    private void StartDurationTimer()
    {
        if (!durationTimer.IsRunning)
        {
            durationTimer.Start();
        }
    }

    private void StopDurationTimer() => durationTimer?.Stop();

    private static string FormatCallDuration(TimeSpan elapsed)
    {
        elapsed = elapsed.Duration();
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }

        return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
    }

    [RelayCommand]
    private async Task EndCallAsync()
    {
        await PhoneCall.EndAsync();
    }

    [RelayCommand]
    private async Task ToggleAudioRouteAsync()
    {
        var next = PhoneCall.AudioDevice is CallingPhoneCallAudioDevice.RemoteDevice
            ? CallingPhoneCallAudioDevice.LocalDevice
            : CallingPhoneCallAudioDevice.RemoteDevice;

        await PhoneCall.ChangeAudioDeviceAsync(next);
    }

    [RelayCommand]
    private async Task ToggleHoldAsync()
    {
        if (!CanToggleHold)
        {
            return;
        }

        if (PhoneCall.Status is CallingPhoneCallStatus.Held)
        {
            await PhoneCall.ResumeFromHoldAsync();
            return;
        }

        await PhoneCall.HoldAsync();
    }

    [RelayCommand]
    private async Task ToggleMuteAsync()
    {
        if (!CanToggleMute)
        {
            return;
        }

        if (PhoneCall.IsMuted)
        {
            await PhoneCall.UnmuteAsync();
            return;
        }

        await PhoneCall.MuteAsync();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        PhoneCall.StatusChanged -= OnCallStatusChanged;
        PhoneCall.AudioDeviceChanged -= OnCallAudioDeviceChanged;
        PhoneCall.IsMutedChanged -= OnCallIsMutedChanged;
        StopDurationTimer();
        durationTimer.Tick -= OnCallDurationTick;
        PhoneCall.Dispose();
    }
}
