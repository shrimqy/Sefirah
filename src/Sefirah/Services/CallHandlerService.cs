using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Utils;
using Sefirah.ViewModels;
using Sefirah.Views.WindowViews;

namespace Sefirah.Services;

public class CallHandlerService(
    IPlatformNotificationHandler platformNotificationHandler,
    IPhoneLineService phoneLineService,
    CallLogRepository callLogRepository,
    ContactRepository contactRepository,
    ILogger logger) : ICallHandler, ICallManager
{
    private CallWindow? ActiveCallWindow { get; set; }
    public CallSessionViewModel? PrimaryCall { get; private set; }
    public CallSessionViewModel? SecondaryCall { get; private set; }

    public event EventHandler? ActiveCallChanged;

    public Task Initialize()
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

            Uri? icon = null;
            if (!string.IsNullOrEmpty(callInfo.ContactInfo?.PhotoBase64))
            {
                icon = await IconUtils.SaveBase64ToFileAsync(callInfo.ContactInfo.PhotoBase64, "callContact.png");
            }

            var title = callInfo.CallState is CallState.Ringing ? "CallIncoming".GetLocalizedResource() : "CallMissed".GetLocalizedResource();
            await platformNotificationHandler.ShowCallNotification(title, GetDisplayText(callInfo), callTag, callInfo.CallState, icon);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling call info from device {DeviceId}", device.Id);
        }
    }

    private static string BuildCallTag(string deviceId, string phoneNumber) => $"call_{deviceId}_{phoneNumber}";

    private static string GetDisplayText(CallInfo callInfo) =>
        !string.IsNullOrWhiteSpace(callInfo.ContactInfo?.DisplayName) ? callInfo.ContactInfo.DisplayName : callInfo.PhoneNumber;

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
                    var (displayName, number, avatarUri) = await GetContactInfoAsync(call);
                    // dispose call object since we don't need this anymore
                    call.Dispose();
                    try
                    {
                        await platformNotificationHandler.ShowCallNotification(callId, title, displayName, avatarUri);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to show incoming call notification for {CallId}.", callId);
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
                            logger.LogError(ex, "Failed to create call session for {CallId}.", callId);
                            call.Dispose();
                        }
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to handle call state change for {CallId}.", callId);
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

    public async Task HandleCallLogInfoAsync(PairedDevice device, CallLogInfo callLogInfo)
    {
        await callLogRepository.SaveCallLogAsync(device.Id, callLogInfo);
    }

    private void UpdateActiveCallWindowSize()
    {
        ActiveCallWindow?.UpdateWindowSize(SecondaryCall is not null);
    }

    private async Task<CallerContact> GetContactAsync(IPhoneCall call)
    {
        var info = await call.GetPhoneCallInfoAsync();
        var number = info.PhoneNumber;

        var contact = contactRepository.GetCallerContactByPhoneNumber(number);
        if (contact is null)
        {
            var displayName = !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : null;
            return new CallerContact(number, displayName);
        }

        return contact;
    }

    private async Task<(string displayName, string phoneNumber, Uri? avatarUri)> GetContactInfoAsync(IPhoneCall call)
    {
        var info = await call.GetPhoneCallInfoAsync();
        var number = info.PhoneNumber;

        var contactEntity = await contactRepository.GetContactByPhoneNumberAsync(number);

        if (contactEntity is null)
        {
            var displayName = !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : number;
            return (displayName, number, null);
        }

        var avatarUri = contactEntity.Avatar is not null ? await IconUtils.SaveBase64ToFileAsync(Convert.ToBase64String(contactEntity.Avatar), "incomingCallContact.png") : null;
        return (contactEntity.DisplayName, number, avatarUri);
    }
}
