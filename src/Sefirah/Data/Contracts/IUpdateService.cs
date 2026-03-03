namespace Sefirah.Data.Contracts;

public interface IUpdateService : INotifyPropertyChanged
{
    /// <summary>
    /// Gets a value indicating whether updates are available.
    /// </summary>
    bool IsUpdateAvailable { get; }

    /// <summary>
    /// Gets a value indicating whether an update is currently being downloaded or installed.
    /// </summary>
    bool IsUpdating { get; }

    Task DownloadUpdatesAsync();
    Task CheckForUpdatesAsync();
}
