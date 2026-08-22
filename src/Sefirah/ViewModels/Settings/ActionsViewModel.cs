using System.Collections.Specialized;
using Sefirah.Actions;
using Sefirah.Dialogs;

namespace Sefirah.ViewModels.Settings;

public sealed partial class ActionsViewModel : BaseViewModel
{
    private readonly IGeneralSettingsService generalSettings = Ioc.Default.GetRequiredService<IUserSettingsService>().GeneralSettingsService;
    private readonly IActionFeature actionFeature = Ioc.Default.GetRequiredService<IActionFeature>();

    private bool isDragging = true;
    private bool isBulkOperation;

    public ObservableCollection<ActionItem> Actions { get; } = [];

    public IReadOnlyList<ActionMetadata> AvailableActions { get; } = ActionFactory.Actions;

    public ActionsViewModel()
    {
        LoadActions();
        Actions.CollectionChanged += Actions_CollectionChanged;
    }

    private void Actions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (isBulkOperation) return;
        if (isDragging)
        {
            isDragging = false;
            return;
        }
        isDragging = true;

        generalSettings.SetActions(Actions);
        actionFeature.SyncActions();
    }

    private void LoadActions()
    {
        isBulkOperation = true;
        Actions.Clear();

        foreach (var action in generalSettings.Actions)
        {
            Actions.Add(action);
        }

        isBulkOperation = false;
    }

    public void UpdateAction(ActionItem action)
    {
        generalSettings.UpdateAction(action);
        actionFeature.SyncActions();
    }

    [RelayCommand]
    private async Task AddAction(string actionId)
    {
        var metadata = AvailableActions.First(m => m.ActionId == actionId);
        await ShowActionDialogAsync(new ActionItem
        {
            Name = metadata.DisplayName,
            ActionId = metadata.ActionId,
            AskForConfirmation = metadata.AskForConfirmationByDefault,
        }, isNew: true);
    }

    [RelayCommand]
    private async Task EditAction(ActionItem action)
    {
        await ShowActionDialogAsync(action.Clone(), isNew: false, replace: action);
    }

    private async Task ShowActionDialogAsync(ActionItem draft, bool isNew, ActionItem? replace = null)
    {
        var dialog = new ActionDialog(draft, isNew)
        {
            XamlRoot = App.MainWindow.Content!.XamlRoot
        };

        if (await dialog.ShowAsync() is not ContentDialogResult.Primary || dialog.Result is null)
        {
            return;
        }

        isBulkOperation = true;
        if (replace is not null)
        {
            var index = Actions.IndexOf(replace);
            if (index >= 0)
            {
                Actions[index] = dialog.Result;
            }

            generalSettings.UpdateAction(dialog.Result);
        }
        else
        {
            Actions.Add(dialog.Result);
            generalSettings.AddAction(dialog.Result);
        }
        isBulkOperation = false;
        actionFeature.SyncActions();
    }

    [RelayCommand]
    private async Task RemoveAction(ActionItem action)
    {
        var dialog = new ContentDialog
        {
            Title = "Remove Action",
            Content = $"Are you sure you want to remove the action '{action.Name}'?",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content!.XamlRoot
        };

        if (await dialog.ShowAsync() is ContentDialogResult.Primary)
        {
            isBulkOperation = true;
            Actions.Remove(action);
            isBulkOperation = false;
            generalSettings.RemoveAction(action);
            actionFeature.SyncActions();
        }
    }
}
