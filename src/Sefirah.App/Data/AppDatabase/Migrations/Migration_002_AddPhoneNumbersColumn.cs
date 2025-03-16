using Microsoft.Data.Sqlite;

namespace Sefirah.App.Data.AppDatabase.Migrations;

public class Migration_002_AddPhoneNumbersColumn : IMigration
{
    public int Version => 2;
    public string Description => "Add PhoneNumbers column to RemoteDevice table";

    public async Task UpAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE RemoteDevice ADD COLUMN PhoneNumbers NVARCHAR(2048)";
        await command.ExecuteNonQueryAsync();
    }
} 