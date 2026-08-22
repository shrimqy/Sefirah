using Sefirah.Utils;

namespace Sefirah.Actions.Power;

public sealed partial class PowerActionSettings : UserControl
{
    private sealed record PowerKindOption(PowerKind Kind, string Name);

    private readonly ActionItem item;

    public PowerActionSettings(ActionItem item)
    {
        this.item = item;
        InitializeComponent();

        var kind = item.Get<PowerSettings>().Kind;
        var options = Enum.GetValues<PowerKind>()
            .Select(k => new PowerKindOption(k, PowerAction.GetName(k)))
            .ToList();

        PowerOptions.ItemsSource = options;
        PowerOptions.DisplayMemberPath = nameof(PowerKindOption.Name);
        PowerOptions.SelectedItem = options.First(o => o.Kind == kind);

        ApplyKind(kind);
    }

    private void PowerOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PowerOptions.SelectedItem is not PowerKindOption { Kind: var kind })
        {
            return;
        }

        ApplyKind(kind);
    }

    private void ApplyKind(PowerKind kind)
    {
        item.Set(new PowerSettings { Kind = kind });
        item.Name = PowerAction.GetName(kind);
        item.Icon = null;
    }
}
