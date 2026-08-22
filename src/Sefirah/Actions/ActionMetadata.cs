namespace Sefirah.Actions;

public sealed record ActionMetadata(
    string ActionId,
    string DefaultIcon,
    bool AskForConfirmationByDefault = false)
{
    public string DisplayName => ActionId.GetLocalizedResource();
}
