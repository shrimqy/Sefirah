using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Actions;
using Sefirah.Utils.Serialization;

namespace Sefirah.Services;

public abstract class BaseActionService(
    IGeneralSettingsService generalSettingsService, 
    ISessionManager sessionManager,
    ILogger logger) : IActionService
{
    public virtual Task InitializeAsync()
    {
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        return Task.CompletedTask;
    }

    private void OnConnectionStatusChanged(object? sender, (PairedDevice Device, bool IsConnected) args)
    {
        if (args.IsConnected && args.Device.Session != null)
        {
            var actions = generalSettingsService.Actions;
            foreach (var action in actions)
            {
                var actionMessage = new ActionMessage 
                { 
                    ActionId = action.Id, 
                    ActionName = action.Name 
                };
                sessionManager.SendMessage(args.Device.Session, SocketMessageSerializer.Serialize(actionMessage));
            }
        }
    }

    public virtual void HandleActionMessage(ActionMessage action)
    {
        logger.LogInformation($"Executing action with ID: {action.ActionId}");
        var actionToExecute = generalSettingsService.Actions.FirstOrDefault(a => a.Id == action.ActionId);

        if (actionToExecute is not null && actionToExecute is ProcessAction processAction)
        {
            processAction.ExecuteAsync();
        }
    }
} 
