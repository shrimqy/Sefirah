using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Services;

public class CallHandlerService(IPlatformNotificationHandler platformNotificationHandler, ILogger<CallHandlerService> logger) : ICallHandler
{
    public async Task HandleCallInfoAsync(PairedDevice device, CallInfo callInfo)
    {
        logger.Debug($"CallInfo, device:{device.Name}, ph: {callInfo.PhoneNumber}, call state: {callInfo.CallState}");

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
                icon = await IconUtils.SaveBase64ToFileAsync(callInfo.ContactInfo!.PhotoBase64!, "callContact.png");
            }

            var title = callInfo.CallState is CallState.Ringing ? "CallNotification.IncomingCall".GetLocalizedResource() : "CallNotification.MissedCall".GetLocalizedResource();
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
}
