using Sefirah.Extensions;
using Sefirah.Utils;

namespace Sefirah.Actions.Power;

public sealed class PowerSettings
{
    public PowerKind Kind { get; set; } = PowerKind.Sleep;
}

public sealed partial class PowerAction(ActionItem item) : IAction, IActionSettings
{
    public static ActionMetadata Metadata { get; } = new(
        "Power",
        "PowerButton",
        AskForConfirmationByDefault: true);

    public string DefaultIcon => GetIcon(item.Get<PowerSettings>().Kind);

    public bool IsValid => true;

    public UIElement CreateSettingPanel() => new PowerActionSettings(item);

    public static string GetIcon(PowerKind kind) => kind switch
    {
        PowerKind.Lock => "Lock",
        PowerKind.LogOff => "SignOut",
        PowerKind.Sleep => "QuietHours",
        PowerKind.Hibernate => "Recent",
        PowerKind.Restart => "UpdateRestore",
        PowerKind.Shutdown => "PowerButton",
        _ => "PowerButton",
    };

    public static string GetName(PowerKind kind) => kind.ToString().GetLocalizedResource();
}
