using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;
public interface ICommandService
{
    /// <summary>
    /// Handles a command message.
    /// </summary>
    /// <param name="command">The command message to handle.</param>
    void HandleCommand(CommandMessage command);
}
