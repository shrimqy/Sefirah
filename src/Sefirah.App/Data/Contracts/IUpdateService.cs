namespace Sefirah.App.Data.Contracts;
public interface IUpdateService : INotifyPropertyChanged
{
    /// <summary>
    /// Gets a value indicating whether updates are available.
    /// </summary>
    bool IsUpdateAvailable { get; }

    Task<bool> CheckForUpdatesAsync();
}
