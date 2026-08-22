using System.Text.Json.Nodes;
using Sefirah.Helpers;
using Sefirah.Actions.Run;

namespace Sefirah.Actions;

public sealed partial class ActionItem : ObservableObject
{
    private string id = Guid.NewGuid().ToString();
    public string Id
    {
        get => id;
        set => SetProperty(ref id, value);
    }

    private string name = string.Empty;
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    private string? icon;
    /// <summary>
    /// Persisted icon: catalog Name (e.g. "Lock"), or a file path to resolve a thumbnail from.
    /// </summary>
    public string? Icon
    {
        get => icon;
        set
        {
            if (SetProperty(ref icon, string.IsNullOrWhiteSpace(value) ? null : value))
            {
                OnPropertyChanged(nameof(DisplayIcon));
            }
        }
    }

    private bool askForConfirmation;
    public bool AskForConfirmation
    {
        get => askForConfirmation;
        set => SetProperty(ref askForConfirmation, value);
    }

    private string actionId = RunAction.Metadata.ActionId;
    public string ActionId
    {
        get => actionId;
        set
        {
            if (SetProperty(ref actionId, value))
            {
                action = null;
            }
        }
    }

    private JsonObject settings = [];
    public JsonObject Settings
    {
        get => settings;
        set => SetProperty(ref settings, value ?? []);
    }

    private IAction? action;
    [JsonIgnore]
    public IAction Action => action ??= ActionFactory.Create(this);

    [JsonIgnore]
    public string DisplayIcon => ActionIconHelper.IsKnown(Icon) ? Icon! : Action.DefaultIcon;

    public ActionItem Clone() => new()
    {
        Id = Id,
        Name = Name,
        Icon = Icon,
        AskForConfirmation = AskForConfirmation,
        ActionId = ActionId,
        Settings = Settings.DeepClone().AsObject(),
    };
}
