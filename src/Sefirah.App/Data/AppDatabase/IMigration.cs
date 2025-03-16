using Microsoft.Data.Sqlite;

namespace Sefirah.App.Data.AppDatabase.Migrations;

public interface IMigration
{
    int Version { get; }
    string Description { get; }
    Task UpAsync(SqliteConnection connection);
}
