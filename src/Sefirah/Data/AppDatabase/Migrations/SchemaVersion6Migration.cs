using Sefirah.Data.AppDatabase.Models;

namespace Sefirah.Data.AppDatabase.Migrations;

public class SchemaVersion6Migration : IMigration
{
    public int TargetVersion => 6;

    public void Up(SQLite.SQLiteConnection db)
    {
        db.RunInTransaction(() =>
        {
            db.DropTable<AttachmentEntity>();
            db.DropTable<MessageEntity>();
            db.DropTable<ConversationEntity>();

            db.CreateTable<ConversationEntity>();
            db.CreateTable<MessageEntity>();
            db.CreateTable<AttachmentEntity>();
        });
    }
}
