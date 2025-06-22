using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IActionService
{
    /// <summary>
    /// Handle default actions like shutdown, restart, etc. (platform-specific)
    /// </summary>
    void HandleDefaultCommand(ActionMessage action);
    
    /// <summary>
    /// Handle custom user-defined actions (same across platforms)
    /// </summary>
    void HandleCustomAction(CustomActionMessage action);
}
