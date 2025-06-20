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

        if (db.GetTableInfo(nameof(ApplicationInfoEntity)).Count == 0)
        {
            db.CreateTable<ApplicationInfoEntity>();
        }

        if (db.GetTableInfo(nameof(ContactEntity)).Count == 0)
        {
            db.CreateTable<ContactEntity>();
        }

        if (db.GetTableInfo(nameof(SmsConversationEntity)).Count == 0)
        {
            db.CreateTable<SmsConversationEntity>();
        }

        if (db.GetTableInfo(nameof(TextMessageEntity)).Count == 0)
        {
            db.CreateTable<TextMessageEntity>();
        }

        if (db.GetTableInfo(nameof(SmsAttachmentEntity)).Count == 0)
        {
            db.CreateTable<SmsAttachmentEntity>();
        }

        return db;
    }
}
