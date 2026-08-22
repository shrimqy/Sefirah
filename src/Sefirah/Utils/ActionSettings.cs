using Sefirah.Actions;

namespace Sefirah.Utils;

public static class ActionSettings
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Get<T>(this ActionItem item) where T : new()
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Settings.Count == 0)
        {
            return new T();
        }

        return item.Settings.Deserialize<T>(Options) ?? new T();
    }

    public static void Set<T>(this ActionItem item, T value) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(value);
        item.Settings = JsonSerializer.SerializeToNode(value, Options)?.AsObject() ?? [];
    }
}
