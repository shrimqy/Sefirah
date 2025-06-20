using Sefirah.Platforms.Windows.RemoteStorage.Commands;

namespace Sefirah.Platforms.Windows.Abstractions;
public partial record SyncProviderContext
{
    public required string Id { get; init; }
    public required string RootDirectory { get; init; }
    public required PopulationPolicy PopulationPolicy { get; init; }
    public string AccountId => Id.Split('!', 3)[2];
    public string RemoteKind => "Sftp";
}
