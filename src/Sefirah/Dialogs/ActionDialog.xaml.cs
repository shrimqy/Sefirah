using Sefirah.Actions;

namespace Sefirah.Dialogs;

public sealed partial class ActionDialog : ContentDialog
{
    private readonly IAction action;

    public ActionItem Item { get; }

    public ActionItem? Result { get; private set; }

    public ActionDialog(ActionItem item, bool isNew = false)
    {
        Item = item;
        action = item.Action;
        InitializeComponent();

        Title = isNew ? "Add Action" : "Edit Action";
        AskForConfirmationToggle.IsOn = item.AskForConfirmation;
        if (action is IActionSettings settings)
        {
            ActionSettingsHost.Content = settings.CreateSettingPanel();
        }
        else
        {
            ActionSettingsHost.Visibility = Visibility.Collapsed;
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Item.Name = Item.Name?.Trim() ?? string.Empty;
        Item.AskForConfirmation = AskForConfirmationToggle.IsOn;

        if (string.IsNullOrWhiteSpace(Item.Name) || !action.IsValid)
        {
            args.Cancel = true;
            return;
        }

        Result = Item;
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = null;
    }
}
