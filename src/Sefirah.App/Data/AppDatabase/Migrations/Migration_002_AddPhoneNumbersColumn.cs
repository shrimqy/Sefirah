using Microsoft.Data.Sqlite;

namespace Sefirah.App.Data.AppDatabase.Migrations;

public class Migration_002_AddPhoneNumbersColumn : IMigration
{
    public int Version => 2;
    public string Description => "Add PhoneNumbers column to RemoteDevice table";

    public async Task UpAsync(SqliteConnection connection)
    {
        bool columnExists = false;
        using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA table_info(RemoteDevice)";
            using var reader = await pragmaCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string columnName = reader.GetString(1);  // Column name is at index 1
                if (columnName.Equals("PhoneNumbers", StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (!columnExists)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE RemoteDevice ADD COLUMN PhoneNumbers NVARCHAR(2048)";
            await command.ExecuteNonQueryAsync();
        }
    }
} 