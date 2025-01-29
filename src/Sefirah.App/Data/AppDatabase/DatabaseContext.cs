using Microsoft.Data.Sqlite;
using Windows.Storage;
using static Sefirah.App.Constants;

namespace Sefirah.App.Data.AppDatabase;
public class DatabaseContext(ILogger logger) : IDisposable
{
    private readonly SqliteConnection _connection = new(LocalSettings.ConnectionString);

    public async Task InitializeAsync()
    {
        try
        {
            // Ensure database file exists
            await ApplicationData.Current.LocalFolder.CreateFileAsync("sefirah.db", CreationCollisionOption.OpenIfExists);
            await EnsureConnectionOpenAsync();

            // Create tables
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ApplicationInfo (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AppPackage NVARCHAR(2048) NOT NULL,
                    AppName NVARCHAR(2048),
                    NotificationFilter NVARCHAR(20) NOT NULL,
                    AppIcon BLOB
                );

                CREATE TABLE IF NOT EXISTS RemoteDevice (
                    DeviceId NVARCHAR(128) PRIMARY KEY,
                    DeviceName NVARCHAR(2048) NOT NULL,
                    LastConnected DATETIME,
                    SharedSecret BLOB,
                    WallpaperBytes BLOB
                );

                CREATE TABLE IF NOT EXISTS LocalDevice (
                    DeviceId NVARCHAR(128) PRIMARY KEY,
                    DeviceName NVARCHAR(2048) NOT NULL,
                    PublicKey BLOB,
                    PrivateKey BLOB
                );";

            await command.ExecuteNonQueryAsync();
            logger.Info("Database initialized successfully");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize database", ex);
            throw;
        }
    }

    public async Task<SqliteConnection> GetConnectionAsync()
    {
        await EnsureConnectionOpenAsync();
        return _connection!;
    }

    private async Task EnsureConnectionOpenAsync()
    {
        try
        {
            await _connection.OpenAsync();
        }
        catch (Exception ex)
        {
            logger.Error("Error ensuring database connection", ex);
            throw;
        }
    }


    public void Dispose()
    {
        _connection?.Dispose();
    }
}
