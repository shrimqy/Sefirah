using Sefirah.Platforms.Desktop.Tray.DBus;
using Tmds.DBus.Protocol;

namespace Sefirah.Platforms.Desktop.Tray;

internal sealed class LinuxTrayMenuHandler : DBusHandler, IdbusmenuHandler, IdbusmenuProperties
{
    private static readonly string[] DefaultPropertyNames = ["type", "label", "enabled", "visible"];

    private readonly ILogger logger;
    private readonly Dictionary<int, Action> actions;
    private readonly uint revision = 1;

    public LinuxTrayMenuHandler(DBusConnection connection, ILogger logger, string path)
        : base(connection, path, handlesChildPaths: false)
    {
        this.logger = logger;
        actions = new()
        {
            [1] = App.TrayStartScrcpy,
            [2] = App.TrayToggleWindow,
            [4] = App.TrayExitApplication
        };
    }

    private static string StartScrcpyLabel => "StartScreenMirroring".GetLocalizedResource();
    private static string ShowHideLabel => "ShowHideWindow".GetLocalizedResource();
    private static string ExitLabel => "Exit".GetLocalizedResource();

    uint IdbusmenuProperties.Version => 4;
    string IdbusmenuProperties.TextDirection => "ltr";
    string IdbusmenuProperties.Status => "normal";
    string[] IdbusmenuProperties.IconThemePath => [];

    ValueTask IdbusmenuHandler.HandleGetPropertyAsync(IdbusmenuHandler.GetPropertyContext context)
        => context.Handle(this);

    ValueTask IdbusmenuHandler.HandleGetAllPropertiesAsync(IdbusmenuHandler.GetAllPropertiesContext context)
        => context.Handle(this);

    ValueTask<(uint Revision, (int, Dictionary<string, VariantValue>, VariantValue[]) Layout)> IdbusmenuHandler.GetLayoutAsync(
        int parentId,
        int recursionDepth,
        string[] propertyNames)
    {
        var layout = BuildLayout(parentId, recursionDepth, propertyNames);
        logger.Info($"DBusMenu GetLayout parent={parentId} depth={recursionDepth} items={layout.Item3.Length}");
        return new ValueTask<(uint, (int, Dictionary<string, VariantValue>, VariantValue[]) Layout)>((revision, layout));
    }

    ValueTask<VariantValue> IdbusmenuHandler.GetPropertyAsync(int id, string name)
        => new(GetItemProperty(id, name) ?? VariantValue.Int32(0));

    ValueTask<(int, Dictionary<string, VariantValue>)[]> IdbusmenuHandler.GetGroupPropertiesAsync(int[] ids, string[] propertyNames)
    {
        var result = ids
            .Select(id => (id, GetItemProperties(id, propertyNames)))
            .ToArray();
        logger.Info($"DBusMenu GetGroupProperties ids={ids.Length}");
        return new ValueTask<(int, Dictionary<string, VariantValue>)[]>(result);
    }

    ValueTask<bool> IdbusmenuHandler.AboutToShowAsync(int id) => new(false);

    ValueTask<(int[] UpdatesNeeded, int[] IdErrors)> IdbusmenuHandler.AboutToShowGroupAsync(int[] ids)
        => new(([], []));

    ValueTask IdbusmenuHandler.EventAsync(int id, string eventId, VariantValue data, uint timestamp)
    {
        if (eventId == "clicked")
        {
            logger.Info($"DBusMenu clicked id={id}");
            if (actions.TryGetValue(id, out var action))
                action();
        }

        return default;
    }

    ValueTask<int[]> IdbusmenuHandler.EventGroupAsync((int, string, VariantValue, uint)[] events)
    {
        foreach (var (id, eventId, _, _) in events)
        {
            if (eventId == "clicked" && actions.TryGetValue(id, out var action))
                action();
        }

        return new ValueTask<int[]>([]);
    }

    private static (int, Dictionary<string, VariantValue>, VariantValue[]) BuildLayout(int parentId, int recursionDepth, string[] propertyNames)
    {
        if (parentId != 0)
            return (parentId, [], []);

        if (recursionDepth == 0)
            return (0, [], []);

        return (0, new Dictionary<string, VariantValue>(),
        [
            CreateLayoutItem(1, "standard", StartScrcpyLabel, propertyNames),
            CreateLayoutItem(2, "standard", ShowHideLabel, propertyNames),
            CreateLayoutItem(3, "separator", "", propertyNames),
            CreateLayoutItem(4, "standard", ExitLabel, propertyNames)
        ]);
    }

    private static VariantValue CreateLayoutItem(int id, string type, string label, string[] propertyNames)
    {
        var props = BuildItemProperties(id, type, label, propertyNames);
        return VariantValue.Struct(
            VariantValue.Int32(id),
            new Dict<string, VariantValue>(props).AsVariantValue(),
            VariantValue.ArrayOfVariant(Array.Empty<VariantValue>()));
    }

    private static Dictionary<string, VariantValue> GetItemProperties(int id, string[] propertyNames)
    {
        var (type, label) = id switch
        {
            1 => ("standard", StartScrcpyLabel),
            2 => ("standard", ShowHideLabel),
            3 => ("separator", ""),
            4 => ("standard", ExitLabel),
            _ => ("standard", "")
        };

        return BuildItemProperties(id, type, label, propertyNames);
    }

    private static Dictionary<string, VariantValue> BuildItemProperties(int id, string type, string label, string[] propertyNames)
    {
        var names = propertyNames.Length == 0 ? DefaultPropertyNames : propertyNames;
        var props = new Dictionary<string, VariantValue>();

        foreach (var name in names)
        {
            var value = GetItemProperty(id, type, label, name);
            if (value is not null)
                props[name] = value.Value;
        }

        return props;
    }

    private static VariantValue? GetItemProperty(int id, string name)
    {
        return id switch
        {
            1 => GetItemProperty(id, "standard", StartScrcpyLabel, name),
            2 => GetItemProperty(id, "standard", ShowHideLabel, name),
            3 => GetItemProperty(id, "separator", "", name),
            4 => GetItemProperty(id, "standard", ExitLabel, name),
            _ => null
        };
    }

    private static VariantValue? GetItemProperty(int id, string type, string label, string name) => name switch
    {
        "type" when type == "separator" => "separator",
        "type" => type,
        "label" when type != "separator" => label,
        "enabled" when type != "separator" => true,
        "visible" when type != "separator" => true,
        _ => null
    };
}
