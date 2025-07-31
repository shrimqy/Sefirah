namespace Sefirah.Data.Models.Actions;

[JsonDerivedType(typeof(ProcessAction), typeDiscriminator: "Process")]
public abstract class BaseAction : ObservableObject
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;
} 
