using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IActionFeature : IFeature
{
    /// <summary>
    /// Handle an action invocation from a remote device.
    /// </summary>
    Task HandleActionMessage(ActionInfo action);

    /// <summary>
    /// Push the current action list to all connected devices.
    /// </summary>
    void SyncActions();
}
