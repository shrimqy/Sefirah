using Sefirah.Utils;

namespace Sefirah.Actions.Link;

public sealed partial class LinkActionSettings : UserControl
{
    private readonly ActionItem item;

    public LinkActionSettings(ActionItem item)
    {
        this.item = item;
        InitializeComponent();
        LinkTextBox.Text = item.Get<LinkSettings>().Url;
    }

    private void LinkTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        item.Set(new LinkSettings
        {
            Url = LinkTextBox.Text?.Trim() ?? string.Empty
        });
    }
}
