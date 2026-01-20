using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class SchemaVersionEntity
{
    [PrimaryKey]
    public int Version { get; set; }
}
