using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface ICallHandler
{
    /// <summary>
    /// Handles incoming call info from a device: shows a notification when there is an incoming or active call.
    /// </summary>
    Task HandleCallInfoAsync(PairedDevice device, CallInfo callInfo);
}
