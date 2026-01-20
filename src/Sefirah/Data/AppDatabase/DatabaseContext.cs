using Sefirah.Data.AppDatabase.Migrations;
using Sefirah.Data.AppDatabase.Models;
using SQLite;
namespace Sefirah.Data.AppDatabase;

public class DatabaseContext
{
    private const int CurrentSchemaVersion = 1;

    private static readonly IMigration[] Migrations = [];

    public SQLiteConnection Database { get; private set; }

    public DatabaseContext(ILogger<DatabaseContext> logger)
    {
        try
        {
            logger.LogInformation("Initializing database context");
            Database = TryCreateDatabase(logger);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to initialize database context {ex}", ex);
            throw;
        }
    }

    private static SQLiteConnection TryCreateDatabase(ILogger logger)
    {
        var databasePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "sefirah.db");
        var db = new SQLiteConnection(databasePath);

        db.CreateTable<SchemaVersionEntity>();

        int? storedSchemaVersion = db.Table<SchemaVersionEntity>().FirstOrDefault()?.Version;

        // If schema version doesn't match or doesn't exist, run migrations or recreate
        if (storedSchemaVersion != CurrentSchemaVersion)
        {
            if (storedSchemaVersion.HasValue && storedSchemaVersion < CurrentSchemaVersion)
            {
                RunMigrations(db, storedSchemaVersion.Value, logger);
            }
            else
            {
                // New database
                DestructiveFallback(db);
            }
            
            SetSchemaVersion(db, CurrentSchemaVersion);
            logger.LogInformation("Database schema updated successfully");
        }
        else
        {
            CreateAllTables(db);
        }

        return db;
    }

    private static void SetSchemaVersion(SQLiteConnection db, int version)
    {
        db.InsertOrReplace(new SchemaVersionEntity { Version = version });
    }

    private static void DestructiveFallback(SQLiteConnection db)
    {
        DropAllTables(db);
        CreateAllTables(db);
    }

    private static void RunMigrations(SQLiteConnection db, int fromVersion, ILogger logger)
    {
        var migrationsToRun = Migrations
            .Where(m => m.TargetVersion > fromVersion && m.TargetVersion <= CurrentSchemaVersion)
            .OrderBy(m => m.TargetVersion)
            .ToArray();

        foreach (var migration in migrationsToRun)
        {
            try
            {
                logger.LogInformation("Running migration to version {Version}", migration.TargetVersion);
                migration.Up(db);
                SetSchemaVersion(db, migration.TargetVersion);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Migration to version {Version} failed. Falling back to destructive mode.", migration.TargetVersion);
                DestructiveFallback(db);
                return;
            }
        }
    }

    private static void CreateAllTables(SQLiteConnection db)
    {
        db.CreateTable<SchemaVersionEntity>();
        db.CreateTable<LocalDeviceEntity>();
        db.CreateTable<PairedDeviceEntity>();
        db.CreateTable<ApplicationInfoEntity>();
        db.CreateTable<ContactEntity>();
        db.CreateTable<ConversationEntity>();
        db.CreateTable<MessageEntity>();
        db.CreateTable<AttachmentEntity>();
    }

    private static void DropAllTables(SQLiteConnection db)
    {
        db.DropTable<LocalDeviceEntity>();
        db.DropTable<PairedDeviceEntity>();
        db.DropTable<ApplicationInfoEntity>();
        db.DropTable<ContactEntity>();
        db.DropTable<ConversationEntity>();
        db.DropTable<MessageEntity>();
        db.DropTable<AttachmentEntity>();
        db.DropTable<SchemaVersionEntity>();
    }

}
