using Sefirah.Data.Models.Actions;
using Sefirah.Utils;

namespace Sefirah.Dialogs;

public sealed partial class ProcessActionDialog : ContentDialog
{
    private readonly ProcessAction? action;
    public ProcessAction? Result { get; private set; }

    public bool IsFormValid => !string.IsNullOrWhiteSpace(ActionNameTextBox?.Text) &&
                              !string.IsNullOrWhiteSpace(ActionPathTextBox?.Text);

    public ProcessActionDialog(ProcessAction? processAction = null)
    {
        InitializeComponent();
        action = processAction;
        Title = processAction is null ? "Add New Action" : "Edit Action";

        if (processAction is not null)
        {
            ActionNameTextBox.Text = processAction.Name;
            ActionPathTextBox.Text = processAction.Path;
            ArgumentsTextBox.Text = processAction.Arguments ?? string.Empty;
            StartDirectoryTextBox.Text = processAction.StartInDirectory ?? string.Empty;
        }
    }

    private void OnFormFieldChanged(object sender, TextChangedEventArgs e)
    {
        Bindings.Update();
    }

    private async void BrowseForExecutable_Click(object sender, RoutedEventArgs e)
    {
        if (await PickerHelper.PickFileAsync() is StorageFile file)
        {
            ActionPathTextBox.Text = file.Path;

            if (string.IsNullOrWhiteSpace(ActionNameTextBox.Text))
            {
                ActionNameTextBox.Text = Path.GetFileNameWithoutExtension(file.Path);
            }

            if (string.IsNullOrWhiteSpace(StartDirectoryTextBox.Text))
            {
                StartDirectoryTextBox.Text = Path.GetDirectoryName(file.Path) ?? string.Empty;
            }

            Bindings.Update();
        }
    }

    private async void BrowseForDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (await PickerHelper.PickFolderAsync() is StorageFolder folder)
        {
            StartDirectoryTextBox.Text = folder.Path;
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = new ProcessAction
        {
            Id = action?.Id ?? Guid.NewGuid().ToString(),
            Name = ActionNameTextBox.Text.Trim(),
            Path = ActionPathTextBox.Text.Trim(),
            Arguments = ArgumentsTextBox.Text?.Trim() ?? string.Empty,
            StartInDirectory = StartDirectoryTextBox.Text?.Trim() ?? string.Empty,
            UseShellExecute = action?.UseShellExecute ?? false,
            CreateNoWindow = action?.CreateNoWindow ?? true,
            EnvironmentVariables = action?.EnvironmentVariables ?? []
        };
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = null;
    }
}
