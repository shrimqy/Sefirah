using Sefirah.Data.Models;
using Sefirah.Data.Models.Actions;
using Sefirah.Services;

namespace Sefirah.Features;

public class ActionFeature(
    IGeneralSettingsService generalSettingsService,
    IUserSettingsService userSettingsService,
    ISessionManager sessionManager,
    ILogger logger) : IActionFeature
{
    public Task InitializeAsync()
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

    public void HandleActionMessage(ActionInfo action)
    {
        logger.Info($"Executing action: {action.ActionName}");
        var actionToExecute = generalSettingsService.Actions.FirstOrDefault(a => a.Id == action.ActionId);

        if (actionToExecute is not null && actionToExecute is ProcessAction processAction)
        {
            processAction.ExecuteAsync();
        }
    }
}
