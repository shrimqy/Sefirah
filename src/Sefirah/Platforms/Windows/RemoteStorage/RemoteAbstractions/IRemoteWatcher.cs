namespace Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
public interface IRemoteWatcher : IDisposable
{
    void Start(CancellationToken stoppingToken = default);
    event RemoteCreateHandler? Created;
    event RemoteChangeHandler? Changed;
    event RemoteRenameHandler? Renamed;
    event RemoteDeleteHandler? Deleted;
}

public delegate Task RemoteCreateHandler(string relativePath);
public delegate Task RemoteChangeHandler(string relativePath);
public delegate Task RemoteRenameHandler(string oldRelativePath, string newRelativePath);
public delegate Task RemoteDeleteHandler(string relativePath);
