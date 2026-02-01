using Sefirah.Data.AppDatabase.Models;

namespace Sefirah.Data.AppDatabase.Migrations;

public class AddNotificationEntityMigration : IMigration
{
    public int TargetVersion => 2;

    public void Up(SQLite.SQLiteConnection db)
    {
        db.CreateTable<NotificationEntity>();
    }
}
