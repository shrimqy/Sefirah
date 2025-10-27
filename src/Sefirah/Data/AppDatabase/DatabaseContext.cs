using Sefirah.Data.AppDatabase.Models;
using SQLite;
namespace Sefirah.Data.AppDatabase;

public class DatabaseContext
{
    public SQLiteConnection Database { get; private set; }

    public DatabaseContext(ILogger<DatabaseContext> logger)
    {
        try
        {
            logger.LogInformation("Initializing database context");
            Database = TryCreateDatabase();
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to initialize database context {ex}", ex);
            throw;
        }
    }

    private static SQLiteConnection TryCreateDatabase()
    {
        var databasePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "sefirah.db");
        var db = new SQLiteConnection(databasePath);

        if (db.GetTableInfo(nameof(LocalDeviceEntity)).Count == 0)
        {
            db.CreateTable<LocalDeviceEntity>();
        }

        if (db.GetTableInfo(nameof(RemoteDeviceEntity)).Count == 0)
        {
            db.CreateTable<RemoteDeviceEntity>();
        }
        else
        {
            // Check if Model column exists, if not add it (migration for existing databases)
            var remoteDeviceColumns = db.GetTableInfo(nameof(RemoteDeviceEntity));
            var hasModelColumn = remoteDeviceColumns.Any(col => col.Name.Equals("Model", StringComparison.OrdinalIgnoreCase));
            
            if (!hasModelColumn)
            {
                try
                {
                    db.Execute("ALTER TABLE RemoteDeviceEntity ADD COLUMN Model TEXT DEFAULT ''");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Migration warning: Could not add Model column: {ex.Message}");
                }
            }
        }

        if (db.GetTableInfo(nameof(ApplicationInfoEntity)).Count == 0)
        {
            db.CreateTable<ApplicationInfoEntity>();
        }

        if (db.GetTableInfo(nameof(ContactEntity)).Count == 0)
        {
            db.CreateTable<ContactEntity>();
        }

        if (db.GetTableInfo(nameof(ConversationEntity)).Count == 0)
        {
            db.CreateTable<ConversationEntity>();
        }

        if (db.GetTableInfo(nameof(MessageEntity)).Count == 0)
        {
            db.CreateTable<MessageEntity>();
        }

        if (db.GetTableInfo(nameof(AttachmentEntity)).Count == 0)
        {
            db.CreateTable<AttachmentEntity>();
        }

        return db;
    }
}
