using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.ViewModels;
using Sefirah.Views.WindowViews;

namespace Sefirah.Features;

public class CallFeature(
    IPlatformNotificationHandler platformNotificationHandler,
    IPhoneLineService phoneLineService,
    CallLogRepository callLogRepository,
    ContactRepository contactRepository,
    IDeviceManager deviceManager,
    ILogger logger) : ICallFeature
{
    private CallWindow? ActiveCallWindow { get; set; }
    public CallSessionViewModel? PrimaryCall { get; private set; }
    public CallSessionViewModel? SecondaryCall { get; private set; }

    public event EventHandler? ActiveCallChanged;

    public Task InitializeAsync()
    {
        phoneLineService.CallStateChanged += OnPhoneLineCallStateChanged;
        return Task.CompletedTask;
    }

    public async Task HandleCallInfoAsync(PairedDevice device, CallInfo callInfo)
    {
        if (!string.IsNullOrEmpty(device.CallsTransportDeviceId)) return;

        if (callInfo.CallState is CallState.InProgress)
        {
            var callTag = BuildCallTag(device.Id, callInfo.PhoneNumber);
            await platformNotificationHandler.RemoveNotificationByTag(callTag);
            return;
        }

        try
        {
            var callTag = BuildCallTag(device.Id, callInfo.PhoneNumber);

            if (callInfo.ContactInfo is not null)
                await contactRepository.SaveContactAsync(device.Id, callInfo.ContactInfo);

            var contact = contactRepository.GetContact(
                device.Id,
                callInfo.PhoneNumber,
                callInfo.ContactInfo?.DisplayName);

            var icon = contact.HasAvatar ? await contact.GetToastAvatarUriAsync() : null;
            var title = callInfo.CallState is CallState.Ringing ? "CallIncoming".GetLocalizedResource() : "CallMissed".GetLocalizedResource();
            await platformNotificationHandler.ShowCallNotification(title, contact.DisplayName, callTag, callInfo.CallState, icon);
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling call info from device {device.Id}", ex);
        }
    }

    private static string BuildCallTag(string deviceId, string phoneNumber) => $"call_{deviceId}_{phoneNumber}";

    private async void OnPhoneLineCallStateChanged(object? sender, IPhoneCall call)
    {
        var callId = call.CallId;
        try
        {
            switch (call.Status)
            {
                case CallingPhoneCallStatus.Ended:
                case CallingPhoneCallStatus.Lost:
                    await platformNotificationHandler.RemoveNotificationsByTagAndGroup(callId, Constants.Notification.IncomingPhoneCallGroup);
                    call.Dispose();
                    break;

                case CallingPhoneCallStatus.Incoming:
                {
                    var title = "CallIncoming".GetLocalizedResource();
                    var (displayName, _, avatarUri) = await GetContactInfoAsync(call);
                    var transportDeviceId = call.TransportDeviceId;
                    call.Dispose();
                    try
                    {
                        await platformNotificationHandler.ShowCallNotification(callId, transportDeviceId, title, displayName, avatarUri);
                    }
                    catch (Exception ex)
                    {
                        logger.Debug($"Failed to show incoming call notification for {callId}.", ex);
                    }
                    break;
                }

                default:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
                    {
                        try
                        {
                            if (PrimaryCall?.CallId == callId || SecondaryCall?.CallId == callId)
                            {
                                call.Dispose();
                                return;
                            }

                            CallSessionViewModel? newSession = null;

                            if (PrimaryCall is null)
                            {
                                var callerContact = await GetContactAsync(call);
                                newSession = new CallSessionViewModel(call, callerContact);
                                PrimaryCall = newSession;
                            }
                            else if (SecondaryCall is null)
                            {
                                var callerContact = await GetContactAsync(call);
                                newSession = new CallSessionViewModel(call, callerContact);
                                SecondaryCall = newSession;
                            }
                            else
                            {
                                // more than 2 calls isn't supported
                                call.Dispose();
                                return;
                            }

                            newSession.SessionEnded += OnSessionEnded;

                            // Keep the currently active (non-held) call in the primary slot.
                            // When a second call arrives and becomes active, promote it to primary.
                            if (PrimaryCall is not null && SecondaryCall is not null
                                && PrimaryCall.PhoneCall.Status is CallingPhoneCallStatus.Held
                                && SecondaryCall.PhoneCall.Status is not CallingPhoneCallStatus.Held)
                            {
                                (PrimaryCall, SecondaryCall) = (SecondaryCall, PrimaryCall);
                            }

                            await platformNotificationHandler.RemoveNotificationsByTagAndGroup(callId, Constants.Notification.IncomingPhoneCallGroup);

                            ActiveCallChanged?.Invoke(this, EventArgs.Empty);
                            ActiveCallWindow ??= new CallWindow();
                            UpdateActiveCallWindowSize();
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Failed to create call session for {callId}.", ex);
                            call.Dispose();
                        }
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to handle call state change for {callId}.", ex);
            call.Dispose();
        }
    }

    private void OnSessionEnded(object? sender, EventArgs e)
    {
        var endedCallId = (sender as CallSessionViewModel)?.CallId;
        if (string.IsNullOrWhiteSpace(endedCallId))
        {
            return;
        }

        var sessionsChanged = false;

        if (PrimaryCall?.CallId == endedCallId)
        {
            var disposedPrimary = PrimaryCall;
            disposedPrimary.SessionEnded -= OnSessionEnded;
            disposedPrimary.Dispose();
            PrimaryCall = SecondaryCall;
            SecondaryCall = null;
            sessionsChanged = true;
        }
        else if (SecondaryCall?.CallId == endedCallId)
        {
            var disposedSecondary = SecondaryCall;
            disposedSecondary.SessionEnded -= OnSessionEnded;
            disposedSecondary.Dispose();
            SecondaryCall = null;
            sessionsChanged = true;
        }

        if (!sessionsChanged)
        {
            return;
        }

        ActiveCallChanged?.Invoke(this, EventArgs.Empty);

        if (PrimaryCall is null)
        {
            ActiveCallWindow?.Close();
            ActiveCallWindow = null;
        }
        else
        {
            UpdateActiveCallWindowSize();
        }
    }

    public async Task SwapCallsAsync()
    {
        if (PrimaryCall is null || SecondaryCall is null)
            return;

        if (PrimaryCall.PhoneCall.Status is CallingPhoneCallStatus.Talking)
            await PrimaryCall.PhoneCall.HoldAsync();

        if (SecondaryCall.PhoneCall.Status is CallingPhoneCallStatus.Held)
            await SecondaryCall.PhoneCall.ResumeFromHoldAsync();

        (PrimaryCall, SecondaryCall) = (SecondaryCall, PrimaryCall);
        ActiveCallChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateActiveCallWindowSize()
    {
        ActiveCallWindow?.UpdateWindowSize(SecondaryCall is not null);
    }

    private async Task<Contact> GetContactAsync(IPhoneCall call)
    {
        var info = await call.GetPhoneCallInfoAsync();
        return contactRepository.GetContact(ResolveDeviceId(call), info.PhoneNumber, info.DisplayName);
    }

    private async Task<(string displayName, string phoneNumber, Uri? avatarUri)> GetContactInfoAsync(IPhoneCall call)
    {
        var info = await call.GetPhoneCallInfoAsync();
        var contact = contactRepository.GetContact(ResolveDeviceId(call), info.PhoneNumber, info.DisplayName);
        var avatarUri = contact.HasAvatar ? await contact.GetToastAvatarUriAsync() : null;
        return (contact.DisplayName, info.PhoneNumber, avatarUri);
    }

    private string? ResolveDeviceId(IPhoneCall call) => deviceManager.FindDeviceByTransportId(call.TransportDeviceId)?.Id ?? deviceManager.ActiveDevice?.Id;
}
