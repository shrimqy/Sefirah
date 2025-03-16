using Microsoft.Data.Sqlite;

namespace Sefirah.App.Data.AppDatabase.Migrations;

public class Migration_001_AddIpAddressesColumn : IMigration
{
    public int Version => 1;
    public string Description => "Add IpAddresses column to RemoteDevice table";

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
                if (columnName.Equals("IpAddresses", StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        // Only add the column if it doesn't exist
        if (!columnExists)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE RemoteDevice ADD COLUMN IpAddresses NVARCHAR(2048)";
            await command.ExecuteNonQueryAsync();
        }
    }
} 