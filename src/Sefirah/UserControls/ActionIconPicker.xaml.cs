using Sefirah.Helpers;
using Sefirah.Utils;

namespace Sefirah.UserControls;

public sealed class IconPickItem
{
    public string Key { get; set; } = string.Empty;
    public string Glyph { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = [];
}

public sealed partial class ActionIconPicker : UserControl
{
    private readonly List<IconPickItem> allIcons =
    [
        .. ActionIconHelper.Catalog.Select(icon => new IconPickItem
        {
            Key = icon.Name,
            Glyph = ActionIconHelper.GlyphForIcon(icon.Name),
            Tooltip = icon.Name,
            Tags = icon.Tags
        })
    ];

    public ObservableCollection<IconPickItem> FilteredIcons { get; } = [];

    /// <summary>Currently selected built-in icon Name (custom path leaves selection cleared).</summary>
    public string? SelectedIcon { get; set; }

    public event EventHandler<string>? IconPicked;

    public ActionIconPicker()
    {
        InitializeComponent();
        ApplyFilter(string.Empty);
    }

    public void PrepareForOpen()
    {
        SearchBox.Text = string.Empty;
        ApplyFilter(string.Empty);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason is AutoSuggestionBoxTextChangeReason.UserInput or AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
        {
            ApplyFilter(sender.Text);
        }
    }

    private void ApplyFilter(string? query)
    {
        FilteredIcons.Clear();
        var filter = query?.Trim() ?? string.Empty;
        foreach (var item in allIcons)
        {
            if (filter.Length == 0 ||
                item.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Tags.Any(tag => tag.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredIcons.Add(item);
            }
        }
    }

    private void IconItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: IconPickItem item })
        {
            return;
        }

        SelectedIcon = item.Key;
        IconPicked?.Invoke(this, item.Key);
    }

    private async void BrowseCustomIcon_Click(object sender, RoutedEventArgs e)
    {
        if (await PickerHelper.PickFileAsync([".png", ".jpg", ".jpeg", ".webp", ".ico", ".gif"]) is not StorageFile file)
        {
            return;
        }

        SelectedIcon = file.Path;
        IconPicked?.Invoke(this, file.Path);
    }
}
