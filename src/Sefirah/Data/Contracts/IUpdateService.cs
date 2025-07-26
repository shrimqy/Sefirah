namespace Sefirah.Data.Contracts;
public interface IUpdateService
{
    /// <summary>
    /// Gets a value indicating whether updates are available.
    /// </summary>
    bool IsUpdateAvailable { get; }

    Task DownloadUpdatesAsync();
    Task CheckForUpdatesAsync();
}
