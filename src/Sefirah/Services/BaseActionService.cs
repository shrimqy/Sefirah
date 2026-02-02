using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Actions;

namespace Sefirah.Services;

public abstract class BaseActionService(
    IGeneralSettingsService generalSettingsService,
    IUserSettingsService userSettingsService,
    ISessionManager sessionManager,
    ILogger logger) : IActionService
{
    public virtual Task InitializeAsync()
    {
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        if (ApplicationData.Current.LocalSettings.Values["DefaultActionsLoaded"] is null)
        {
            ApplicationData.Current.LocalSettings.Values["DefaultActionsLoaded"] = true;
            var defaultActions = DefaultActionsProvider.GetDefaultActions();
            userSettingsService.GeneralSettingsService.Actions = [.. defaultActions];
        }

        return Task.CompletedTask;
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (device.IsConnected)
        {
            var actions = generalSettingsService.Actions;
            foreach (var action in actions)
            {
                var actionMessage = new ActionInfo { ActionId = action.Id, ActionName = action.Name };
                device.SendMessage(actionMessage);
            }
        }
    }

    public virtual void HandleActionMessage(ActionInfo action)
    {
        logger.LogInformation("Executing action: {name}", action.ActionName);
        var actionToExecute = generalSettingsService.Actions.FirstOrDefault(a => a.Id == action.ActionId);

        if (actionToExecute is not null && actionToExecute is ProcessAction processAction)
        {
            processAction.ExecuteAsync();
        }
    }
} 
