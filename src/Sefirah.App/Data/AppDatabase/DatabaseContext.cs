using Microsoft.Data.Sqlite;
using Windows.Storage;
using static Sefirah.App.Constants;
using Sefirah.App.Data.AppDatabase.Migrations;

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
                    WallpaperBytes BLOB,
                    PhoneNumbers NVARCHAR(2048)
                );

                CREATE TABLE IF NOT EXISTS LocalDevice (
                    DeviceId NVARCHAR(128) PRIMARY KEY,
                    DeviceName NVARCHAR(2048) NOT NULL,
                    PublicKey BLOB,
                    PrivateKey BLOB
                );";

            await command.ExecuteNonQueryAsync();

            await RunMigrationsAsync();
            logger.Info("Database initialized successfully");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to initialize database", ex);
            throw;
        }
    }

    private async Task RunMigrationsAsync()
    {
        try
        {
            // Create version table if it doesn't exist
            using (var createVersionTableCommand = _connection.CreateCommand())
            {
                createVersionTableCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS SchemaVersion (
                        Version INTEGER PRIMARY KEY,
                        AppliedOn DATETIME NOT NULL
                    );";
                await createVersionTableCommand.ExecuteNonQueryAsync();
            }

            // Get current schema version
            int currentVersion = 0;
            using (var versionCommand = _connection.CreateCommand())
            {
                versionCommand.CommandText = "SELECT MAX(Version) FROM SchemaVersion";
                var result = await versionCommand.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    currentVersion = Convert.ToInt32(result);
                }
            }

            // Apply migrations in order based on current version
            var migrations = GetMigrations();
            foreach (var migration in migrations.Where(m => m.Version > currentVersion).OrderBy(m => m.Version))
            {
                await migration.UpAsync(_connection);

                // Record that migration was applied
                using var insertVersionCommand = _connection.CreateCommand();
                insertVersionCommand.CommandText = "INSERT INTO SchemaVersion (Version, AppliedOn) VALUES (@Version, @AppliedOn)";
                insertVersionCommand.Parameters.AddWithValue("@Version", migration.Version);
                insertVersionCommand.Parameters.AddWithValue("@AppliedOn", DateTime.UtcNow);
                await insertVersionCommand.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            logger.Error("Failed to run migrations", ex);
            throw;
        }
    }

    private static List<IMigration> GetMigrations()
    {
        return
        [
            new Migration_001_AddIpAddressesColumn(),
            new Migration_002_AddPhoneNumbersColumn(),
        ];
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
        GC.SuppressFinalize(this);
    }
}
