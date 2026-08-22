using Sefirah.Actions.Link;
using Sefirah.Actions.Power;
using Sefirah.Actions.Run;

namespace Sefirah.Actions;

public static partial class ActionFactory
{
    private static partial ActionRegistration[] GetActions() =>
    [
        new(RunAction.Metadata, item => new RunAction(item)),
        new(PowerAction.Metadata, item => new PowerAction(item)),
        new(LinkAction.Metadata, item => new LinkAction(item)),
    ];
}
