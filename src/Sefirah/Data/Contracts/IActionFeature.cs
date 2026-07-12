using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IActionFeature : IFeature
{
    /// <summary>
    /// Handle actions
    /// </summary>
    void HandleActionMessage(ActionInfo action);
}
