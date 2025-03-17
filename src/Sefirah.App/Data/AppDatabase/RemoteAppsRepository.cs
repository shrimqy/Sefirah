using Microsoft.Data.Sqlite;
using Sefirah.App.Data.AppDatabase;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;

public class RemoteAppsRepository(DatabaseContext context, ILogger logger) : IRemoteAppsRepository
{
    public async Task<List<ApplicationInfoEntity>> GetAllAsync()
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "SELECT AppPackage, AppName, NotificationFilter, AppIcon FROM ApplicationInfo ORDER BY AppName",
                conn);

            using var reader = await command.ExecuteReaderAsync();
            var applications = new List<ApplicationInfoEntity>();

            while (await reader.ReadAsync())
            {
                var appIconBytes = reader.IsDBNull(3) ? null : (byte[])reader[3];
                var appIcon = appIconBytes != null ? await appIconBytes.ToBitmapAsync() : null;
                
                var notificationFilterStr = reader.GetString(2);
                NotificationFilter notificationFilter = NotificationFilter.ToastFeed;
                
                if (!Enum.TryParse<NotificationFilter>(notificationFilterStr, out notificationFilter))
                {
                    logger.Warn($"Invalid NotificationFilter value '{notificationFilterStr}' for app {reader.GetString(1)}. Using default.");
                }

                applications.Add(new ApplicationInfoEntity
                {
                    AppPackage = reader.GetString(0),
                    AppName = reader.GetString(1),
                    NotificationFilter = notificationFilter,
                    AppIconBytes = appIconBytes,
                    AppIcon = appIcon
                });
            }

            return applications;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting application info list", ex);
            throw;
        }
    }

    public async Task<ObservableCollection<ApplicationInfoEntity>> GetAllAsObservableCollection()
    {
        try 
        {
            using var conn = await context.GetConnectionAsync();
            using var command = new SqliteCommand(
                "SELECT AppPackage, AppName, NotificationFilter, AppIcon FROM ApplicationInfo ORDER BY AppName",
                conn);

            var apps = new ObservableCollection<ApplicationInfoEntity>();

            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
                {
                using var reader = await command.ExecuteReaderAsync();
                {
                    while (reader.Read())
                    {
                        var appIconBytes = reader.IsDBNull(3) ? null : (byte[])reader[3];
                        var appIcon = appIconBytes != null ? await appIconBytes.ToBitmapAsync() : null;

                        var notificationFilterStr = reader.GetString(2);
                        NotificationFilter notificationFilter = NotificationFilter.ToastFeed;
                                
                        if (!Enum.TryParse<NotificationFilter>(notificationFilterStr, out notificationFilter))
                        {
                            logger.Warn($"Invalid NotificationFilter value '{notificationFilterStr}' for app {reader.GetString(1)}. Using default.");
                        }

                        apps.Add(new ApplicationInfoEntity
                        {
                            AppPackage = reader.GetString(0),
                            AppName = reader.GetString(1),
                            NotificationFilter = notificationFilter,
                            AppIconBytes = appIconBytes,
                            AppIcon = appIcon
                        });
                    }  
                }
            }
            return apps;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting application info list", ex);
            return null;
        }
    }

    public async Task<ObservableCollection<ApplicationInfoEntity>> GetInstalledAppsAsync()
    {
        try 
        {
            using var conn = await context.GetConnectionAsync();
            using var command = new SqliteCommand(
                "SELECT AppPackage, AppName, AppIcon FROM ApplicationInfo ORDER BY AppName",
                conn);

            var apps = new ObservableCollection<ApplicationInfoEntity>();

            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
                {
                using var reader = await command.ExecuteReaderAsync();
                {
                    while (reader.Read())
                    {
                        var appIconBytes = reader.IsDBNull(2) ? null : (byte[])reader[2];
                        var appIcon = appIconBytes != null ? await appIconBytes.ToBitmapAsync() : null;

                        apps.Add(new ApplicationInfoEntity
                        {
                            AppPackage = reader.GetString(0),
                            AppName = reader.GetString(1),
                            AppIconBytes = appIconBytes,
                            AppIcon = appIcon
                        });
                    }  
                }
            }
            return apps;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting application info list", ex);
            return null;
        }
    }

    public async Task<ApplicationInfoEntity?> GetByAppPackageAsync(string appPackage)
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "SELECT AppPackage, AppName, NotificationFilter, AppIcon FROM ApplicationInfo WHERE AppPackage = @AppPackage",
                conn);

            command.Parameters.AddWithValue("@AppPackage", appPackage);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var notificationFilterStr = reader.GetString(2);
                NotificationFilter notificationFilter = NotificationFilter.ToastFeed;
                
                if (!Enum.TryParse<NotificationFilter>(notificationFilterStr, out notificationFilter))
                {
                    logger.Warn($"Invalid NotificationFilter value '{notificationFilterStr}' for app {reader.GetString(1)}. Using default.");
                }
                
                return new ApplicationInfoEntity
                {
                    AppPackage = reader.GetString(0),
                    AppName = reader.GetString(1),
                    NotificationFilter = notificationFilter,
                    AppIconBytes = reader.IsDBNull(3) ? null : (byte[])reader[3]
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting application info", ex);
            throw;
        }
    }

    public async Task AddOrUpdateAsync(ApplicationInfoEntity appInfo)
    {
        try
        {
            if (string.IsNullOrEmpty(appInfo.AppPackage))
            {
                logger.Warn("AppPackage cannot be null or empty");
                return;
            }
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "INSERT OR REPLACE INTO ApplicationInfo (AppPackage, AppName, NotificationFilter, AppIcon) " +
                "VALUES (@AppPackage, @AppName, @NotificationFilter, @AppIcon)",
                conn);

            command.Parameters.AddWithValue("@AppPackage", appInfo.AppPackage);
            command.Parameters.AddWithValue("@AppName", appInfo.AppName);
            command.Parameters.AddWithValue("@NotificationFilter", appInfo.NotificationFilter.ToString());
            command.Parameters.AddWithValue("@AppIcon", appInfo.AppIconBytes as object ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.Error("Error adding/updating application info", ex);
            throw;
        }
    }

    public async Task<NotificationFilter> AddNewAppNotificationFilter(string appPackage, string appName, byte[] appIcon)
    {
        var appInfo = new ApplicationInfoEntity {
            AppPackage = appPackage,
            AppName = appName,
            NotificationFilter = NotificationFilter.ToastFeed,
            AppIconBytes = appIcon
        };
        await AddOrUpdateAsync(appInfo);
        return appInfo.NotificationFilter;
    }

    public async Task<NotificationFilter?> GetNotificationFilterAsync(string appPackage)
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "SELECT NotificationFilter FROM ApplicationInfo WHERE AppPackage = @AppPackage",
                conn);

            command.Parameters.AddWithValue("@AppPackage", appPackage);
            var filter = await command.ExecuteScalarAsync();

            if (Enum.TryParse(filter?.ToString(), out NotificationFilter notificationFilter))
            {
                return notificationFilter;
            }
            return null;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting notification filter", ex);
            throw;
        }
    }   

    public async Task UpdateFilterAsync(string appPackage, NotificationFilter filter)
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "UPDATE ApplicationInfo SET NotificationFilter = @Filter WHERE AppPackage = @AppPackage",
                conn);

            command.Parameters.AddWithValue("@Filter", filter.ToString());
            command.Parameters.AddWithValue("@AppPackage", appPackage);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.Error("Error updating notification filter", ex);
            throw;
        }
    }

    public async Task DeleteAsync(string appPackage)
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "DELETE FROM ApplicationInfo WHERE AppPackage = @AppPackage",
                conn);

            command.Parameters.AddWithValue("@AppPackage", appPackage);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.Error("Error deleting application info", ex);
            throw;
        }
    }

    public async Task ClearAllAsync()
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "DELETE FROM ApplicationInfo",
                conn);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.Error("Error clearing all application info", ex);
            throw;
        }
    }
} 