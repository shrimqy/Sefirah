namespace Sefirah.Data.Contracts;

public interface IPhoneCall : IDisposable
{
    string CallId { get; }

    CallingPhoneCallStatus Status { get; }

    CallingPhoneCallAudioDevice AudioDevice { get; }

    bool IsMuted { get; }

    event EventHandler? AudioDeviceChanged;

    event EventHandler? StatusChanged;

    event EventHandler? IsMutedChanged;

    Task<CallingPhoneCallOperationStatus> ChangeAudioDeviceAsync(CallingPhoneCallAudioDevice device);

    Task<CallingPhoneCallOperationStatus> AcceptIncomingAsync();

    Task<CallingPhoneCallOperationStatus> RejectIncomingAsync();

    Task<CallingPhoneCallOperationStatus> EndAsync();

    Task<CallingPhoneCallOperationStatus> HoldAsync();

    Task<CallingPhoneCallOperationStatus> ResumeFromHoldAsync();

    Task<CallingPhoneCallOperationStatus> MuteAsync();

    Task<CallingPhoneCallOperationStatus> UnmuteAsync();

    Task<IPhoneCallInfo> GetPhoneCallInfoAsync();
}
