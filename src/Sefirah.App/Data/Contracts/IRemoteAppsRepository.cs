using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Enums;

namespace Sefirah.App.Data.Contracts;
public interface IRemoteAppsRepository
{
    /// <summary>
    /// Gets all the remote apps.
    /// </summary>
    Task<List<ApplicationInfoEntity>> GetAllAsync();

    /// <summary>
    /// Gets a remote app by its package name.
    /// </summary>
    Task<ApplicationInfoEntity?> GetByAppPackageAsync(string appPackage);

    /// <summary>
    /// Adds or updates a remote app.
    /// </summary>
    Task AddOrUpdateAsync(ApplicationInfoEntity app);

    /// <summary>
    /// Adds a new remote app notification filter.
    /// </summary>
    Task<NotificationFilter> AddNewAppNotificationFilter(string appPackage, string appName, byte[] appIcon);

    /// <summary>
    /// Gets the notification filter for a remote app.
    /// </summary>
    Task<NotificationFilter?> GetNotificationFilterAsync(string appPackage);

    /// <summary>
    /// Updates the notification filter for a remote app.
    /// </summary>
    Task UpdateFilterAsync(string appPackage, NotificationFilter filter);

    /// <summary>
    /// Deletes a remote app.
    /// </summary>
    Task DeleteAsync(string appPackage);

    /// <summary>
    /// Gets all the remote apps as an observable collection.
    /// </summary>
    Task<ObservableCollection<ApplicationInfoEntity>> GetAllAsObservableCollection();
}
