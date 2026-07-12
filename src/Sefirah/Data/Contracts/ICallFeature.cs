using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.Data.Contracts;

public interface ICallFeature : IFeature
{
    /// <summary>
    /// Handles incoming call info from a device: shows a notification when there is an incoming or active call.
    /// </summary>
    Task HandleCallInfoAsync(PairedDevice device, CallInfo callInfo);

    CallSessionViewModel? PrimaryCall { get; }

    CallSessionViewModel? SecondaryCall { get; }

    event EventHandler? ActiveCallChanged;

    Task SwapCallsAsync();
}
