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
                    AppPackage NVARCHAR(2048) PRIMARY KEY,
                    AppName NVARCHAR(2048),
                    NotificationFilter NVARCHAR(20) NOT NULL,
                    AppIcon BLOB
                );

                CREATE TABLE IF NOT EXISTS RemoteDevice (
                    DeviceId NVARCHAR(128) PRIMARY KEY,
                    DeviceName NVARCHAR(2048) NOT NULL,
                    IpAddresses NVARCHAR(2048),
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
            
            // Should probably use a version based migration system later
            await MigrateAddIpAddressesColumn();
            
            logger.Info("Database initialized successfully");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize database", ex);
            throw;
        }
    }

    private async Task MigrateAddIpAddressesColumn()
    {
        try
        {
            // Check if the column exists
            bool columnExists = false;
            using (var pragmaCommand = _connection.CreateCommand())
            {
                pragmaCommand.CommandText = "PRAGMA table_info(RemoteDevice)";
                using var reader = await pragmaCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string columnName = reader.GetString(1);  // Column name is at index 1
                    if (columnName.Equals("IpAddresses", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            // Add the column if it doesn't exist
            if (!columnExists)
            {
                using var alterCommand = _connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE RemoteDevice ADD COLUMN IpAddresses NVARCHAR(2048)";
                await alterCommand.ExecuteNonQueryAsync();
                logger.Info("Added IpAddresses column to RemoteDevice table");
            }
        }
        catch (Exception ex)
        {
            logger.Error("Failed to migrate IpAddresses column", ex);
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
