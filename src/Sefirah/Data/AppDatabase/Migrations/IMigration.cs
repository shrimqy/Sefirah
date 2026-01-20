namespace Sefirah.Data.AppDatabase.Migrations;

public interface IMigration
{
    int TargetVersion { get; }

    void Up(SQLite.SQLiteConnection db);
}
