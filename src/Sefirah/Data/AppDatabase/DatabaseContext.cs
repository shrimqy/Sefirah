using Sefirah.Data.AppDatabase.Migrations;
using Sefirah.Data.AppDatabase.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase;

public class DatabaseContext
{
    private const int CurrentSchemaVersion = 5;

    private static readonly IMigration[] Migrations = 
    [
        new SchemaVersion2Migration(),
    ];

    public SQLiteConnection Database { get; private set; }

    public DatabaseContext(ILogger<DatabaseContext> logger)
    {
        try
        {
            logger.Info("Initializing database context");
            Database = TryCreateDatabase(logger);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to initialize database context", ex);
            throw;
        }
    }

    private static SQLiteConnection TryCreateDatabase(ILogger logger)
    {
        var databasePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, Constants.LocalSettings.DatabaseFileName);
        var db = new SQLiteConnection(databasePath)
        {
            BusyTimeout = TimeSpan.FromSeconds(5),
        };

        db.CreateTable<SchemaVersionEntity>();

        int? storedSchemaVersion = db.Table<SchemaVersionEntity>()
            .OrderByDescending(v => v.Version)
            .FirstOrDefault()?.Version;

        // If schema version doesn't match, run migrations when they exist; otherwise destructive fallback
        if (storedSchemaVersion != CurrentSchemaVersion)
        {
            if (storedSchemaVersion.HasValue && storedSchemaVersion < CurrentSchemaVersion)
            {
                var migrationsToRun = Migrations
                    .Where(m => m.TargetVersion > storedSchemaVersion.Value && m.TargetVersion <= CurrentSchemaVersion)
                    .OrderBy(m => m.TargetVersion)
                    .ToArray();

                if (migrationsToRun.Length == 0)
                {
                    // No migration path exists (e.g. no migration to current version) → destructive fallback
                    DestructiveFallback(db);
                }
                else
                {
                    RunMigrations(db, migrationsToRun, logger);
                }
            }
            else
            {
                DestructiveFallback(db);
            }

            SetSchemaVersion(db, CurrentSchemaVersion);
            logger.Info("Database schema updated successfully");
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

    private static void RunMigrations(SQLiteConnection db, IMigration[] migrationsToRun, ILogger logger)
    {
        foreach (var migration in migrationsToRun)
        {
            try
            {
                logger.Info($"Running migration to version {migration.TargetVersion}");
                migration.Up(db);
                SetSchemaVersion(db, migration.TargetVersion);
            }
            catch (Exception ex)
            {
                logger.Error($"Migration to version {migration.TargetVersion} failed. Falling back to destructive mode.", ex);
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
        db.CreateTable<ApplicationEntity>();
        db.CreateTable<ContactEntity>();
        db.CreateTable<PhoneNumberEntity>();
        db.CreateTable<ConversationEntity>();
        db.CreateTable<MessageEntity>();
        db.CreateTable<AttachmentEntity>();
        db.CreateTable<CallLogEntity>();
        db.CreateTable<NotificationEntity>();
    }

    public static void DropAllTables(SQLiteConnection db)
    {
        db.DropTable<LocalDeviceEntity>();
        db.DropTable<PairedDeviceEntity>();
        db.DropTable<ApplicationEntity>();
        db.DropTable<PhoneNumberEntity>();
        db.DropTable<ContactEntity>();
        db.DropTable<ConversationEntity>();
        db.DropTable<MessageEntity>();
        db.DropTable<AttachmentEntity>();
        db.DropTable<CallLogEntity>();
        db.DropTable<NotificationEntity>();
        db.DropTable<SchemaVersionEntity>();
    }
}
