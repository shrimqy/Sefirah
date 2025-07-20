using System.Collections.Specialized;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models.Actions;
using Sefirah.Dialogs;
using Sefirah.Services;

namespace Sefirah.ViewModels.Settings;

public sealed partial class ActionsViewModel : BaseViewModel
{
    private readonly IUserSettingsService _userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();

    private bool isDragging = true;
    private bool isBulkOperation;

    public ObservableCollection<BaseAction> Actions { get; } = [];

    public ActionsViewModel()
    {
        LoadActions();
        Actions.CollectionChanged += Actions_CollectionChanged;
    }

    private void Actions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (isBulkOperation) return;
        // Reordering ListView has no events, but its collection is updated twice,
        // first to remove the selected item, and second to add the item at the selected position.
        if (isDragging)
        {
            isDragging = false;
            return;
        }
        isDragging = true;
        
        SaveActions();
    }

    private void LoadActions()
    {
        if (ApplicationData.Current.LocalSettings.Values["DefaultActionsLoaded"] == null) 
        {
            ApplicationData.Current.LocalSettings.Values["DefaultActionsLoaded"] = true;
            var defaultActions = DefaultActionsProvider.GetDefaultActions();
            foreach (var action in defaultActions)
            {
                Actions.Add(action);
            }
            SaveActions();
        }
        isBulkOperation = true;
        Actions.Clear();
        
        var customActions = _userSettingsService.GeneralSettingsService.Actions;
        foreach (var action in customActions)
        {
            Actions.Add(action);
        }
        isBulkOperation = false;
    }

    private void SaveActions()
    {
        _userSettingsService.GeneralSettingsService.Actions = Actions.ToList();
    }

    [RelayCommand]
    private async Task AddAction()
    {
        var dialog = new ProcessActionDialog()
        {
            XamlRoot = App.MainWindow!.Content!.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is not null)
        {
            isBulkOperation = true;
            Actions.Add(dialog.Result);
            isBulkOperation = false;
            SaveActions();
        }
    }

    [RelayCommand]
    private async Task EditAction(BaseAction? action)
    {
        if (action is not IActionDialog actionDialog) return;

        if (await actionDialog.ShowDialogAsync(App.MainWindow!.Content!.XamlRoot!) is { } result)
        {
            _userSettingsService.GeneralSettingsService.UpdateAction(result);
            var existingAction = Actions.First(a => a.Id == result.Id);
            var index = Actions.IndexOf(existingAction!);
            Actions[index] = result;
        }
    }

    [RelayCommand]
    private async Task RemoveAction(BaseAction? action)
    {
        if (action is null) return;

        var dialog = new ContentDialog
        {
            Title = "Remove Action",
            Content = $"Are you sure you want to remove the action '{action.Name}'?",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow!.Content!.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            isBulkOperation = true;
            Actions.Remove(action);
            isBulkOperation = false;
            SaveActions();
        }
    }
} 
