using Sefirah.Helpers;
using Sefirah.Utils;

namespace Sefirah.Actions.Run;

public sealed partial class RunActionSettings : UserControl
{
    private readonly ActionItem item;

    public RunActionSettings(ActionItem item)
    {
        this.item = item;
        InitializeComponent();

        var settings = item.Get<RunSettings>();
        ActionPathTextBox.Text = settings.Path;
        ArgumentsTextBox.Text = settings.Arguments ?? string.Empty;
        StartDirectoryTextBox.Text = settings.StartInDirectory ?? string.Empty;

        // Run action icon is the file path.
        if (!string.IsNullOrWhiteSpace(settings.Path) &&
            (string.IsNullOrEmpty(item.Icon) || ActionIconHelper.IsIconPath(item.Icon)))
        {
            item.Icon = settings.Path;
        }
    }

    private void Field_TextChanged(object sender, TextChangedEventArgs e)
    {
        var settings = item.Get<RunSettings>();
        settings.Path = ActionPathTextBox.Text?.Trim() ?? string.Empty;
        settings.Arguments = ArgumentsTextBox.Text?.Trim() ?? string.Empty;
        settings.StartInDirectory = StartDirectoryTextBox.Text?.Trim() ?? string.Empty;
        item.Set(settings);

        if (ReferenceEquals(sender, ActionPathTextBox))
        {
            item.Icon = string.IsNullOrEmpty(settings.Path) ? null : settings.Path;
        }
    }

    private async void BrowseForPath_Click(object sender, RoutedEventArgs e)
    {
        if (await PickerHelper.PickFileAsync() is not StorageFile file)
        {
            return;
        }

        ActionPathTextBox.Text = file.Path;
        item.Icon = file.Path;

        if (string.IsNullOrWhiteSpace(StartDirectoryTextBox.Text))
        {
            StartDirectoryTextBox.Text = Path.GetDirectoryName(file.Path) ?? string.Empty;
        }
    }

    private async void BrowseForDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (await PickerHelper.PickFolderAsync() is StorageFolder folder)
        {
            StartDirectoryTextBox.Text = folder.Path;
        }
    }
}
