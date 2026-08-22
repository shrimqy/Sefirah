using Sefirah.Actions;
using Sefirah.Data.Models;
using Sefirah.Helpers;

namespace Sefirah.Features;

public class ActionFeature(
    IGeneralSettingsService generalSettingsService,
    ISessionManager sessionManager,
    ILogger logger) : IActionFeature
{
    public Task InitializeAsync()
    {
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        return Task.CompletedTask;
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (device.IsConnected)
        {
            SyncActionsToDevice(device);
        }
    }

    public async void SyncActions()
    {
        try
        {
            sessionManager.BroadcastMessage(await BuildActionListAsync());
        }
        catch (Exception ex)
        {
            logger.Error("Failed to sync actions", ex);
        }
    }

    private async void SyncActionsToDevice(PairedDevice device)
    {
        try
        {
            device.SendMessage(await BuildActionListAsync());
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to sync actions to {device.Name}", ex);
        }
    }

    private async Task<ActionList> BuildActionListAsync()
    {
        var actions = new List<ActionInfo>();
        foreach (var item in generalSettingsService.Actions)
        {
            actions.Add(await ToActionInfoAsync(item));
        }

        return new ActionList { Actions = actions };
    }

    private static async Task<ActionInfo> ToActionInfoAsync(ActionItem item) => new()
    {
        ActionId = item.Id,
        ActionName = item.Name,
        Icon = await ResolveIcon(item),
        AskForConfirmation = item.AskForConfirmation
    };

    /// <summary>
    /// Catalog Name as-is. File path → thumbnail/image bytes. Otherwise default glyph Name.
    /// </summary>
    private static async Task<string> ResolveIcon(ActionItem item)
    {
        if (ActionIconHelper.IsIconPath(item.Icon))
        {
            return await ActionIconHelper.EncodeFromPathAsync(item.Icon!)
                ?? item.Action.DefaultIcon;
        }

        if (ActionIconHelper.IsKnown(item.Icon))
        {
            return item.Icon!;
        }

        return item.Action.DefaultIcon;
    }

    public async Task HandleActionMessage(ActionInfo action)
    {
        logger.Info($"Executing action: {action.ActionName}");
        var item = generalSettingsService.Actions.FirstOrDefault(a => a.Id == action.ActionId);

        if (item is null)
        {
            logger.Warn($"Action not found: {action.ActionId}");
            return;
        }

        try
        {
            await item.Action.ExecuteAsync();
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to execute action '{item.Name}'", ex);
        }
    }
}
