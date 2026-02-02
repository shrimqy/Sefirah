using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IActionService
{
    /// <summary>
    /// Handle actions
    /// </summary>
    void HandleActionMessage(ActionInfo action);

    /// <summary>
    /// Initializes the service.
    /// </summary>
    Task InitializeAsync();
}
