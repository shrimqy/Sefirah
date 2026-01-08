using Sefirah.Data.Models;

namespace Sefirah.Utils.Serialization;

public static class JsonMessageSerializer
{
    private static readonly JsonSerializerOptions options = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(object message) => 
        JsonSerializer.Serialize(message, options);

    public static SocketMessage? DeserializeMessage(string json) => 
        JsonSerializer.Deserialize<SocketMessage>(json, options);
}

