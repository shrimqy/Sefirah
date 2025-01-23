namespace Sefirah.App.RemoteStorage.Abstractions;
public record SyncRootInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Directory { get; init; }
    public string Label => $"{Name} - {Id} - {Directory}";
}
