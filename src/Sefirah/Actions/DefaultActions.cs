using Sefirah.Actions.Power;
using Sefirah.Utils;

namespace Sefirah.Actions;

/// <summary>
/// The actions seeded into the catalog on first run, so a fresh install has something usable.
/// </summary>
public static class DefaultActions
{
    private static readonly PowerKind[] kinds =
    [
        PowerKind.Lock,
        PowerKind.Sleep,
        PowerKind.Shutdown,
        PowerKind.Hibernate,
        PowerKind.Restart,
    ];

    public static List<ActionItem> Create()
    {
        List<ActionItem> items = [with(kinds.Length)];

        foreach (var kind in kinds)
        {
            var item = new ActionItem
            {
                Name = PowerAction.GetName(kind),
                ActionId = PowerAction.Metadata.ActionId,
                AskForConfirmation = PowerAction.Metadata.AskForConfirmationByDefault,
            };

            item.Set(new PowerSettings { Kind = kind });
            items.Add(item);
        }

        return items;
    }
}
