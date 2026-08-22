namespace Sefirah.Actions;

public static partial class ActionFactory
{
    private static readonly ActionRegistration[] actions = GetActions();

    public static IReadOnlyList<ActionMetadata> Actions { get; } =
        [.. actions.Select(r => r.Metadata)];

    public static IAction Create(ActionItem item)
    {
        var registration = actions.FirstOrDefault(r => r.Metadata.ActionId == item.ActionId)
            ?? throw new NotSupportedException($"Unknown action id: {item.ActionId}");

        return registration.Create(item);
    }

    private static partial ActionRegistration[] GetActions();

    internal sealed record ActionRegistration(
        ActionMetadata Metadata,
        Func<ActionItem, IAction> Create);
}
