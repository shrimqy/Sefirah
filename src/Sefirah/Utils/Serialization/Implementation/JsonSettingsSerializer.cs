using System.Text.Json;

namespace Sefirah.Utils.Serialization.Implementation;
internal sealed class JsonSettingsSerializer : IJsonSettingsSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public string? SerializeToJson(object? obj)
    {
        return JsonSerializer.Serialize(obj, Options);
    }

    public T? DeserializeFromJson<T>(string json)
    {
        return JsonSerializer.Deserialize<T?>(json);
    }
}
