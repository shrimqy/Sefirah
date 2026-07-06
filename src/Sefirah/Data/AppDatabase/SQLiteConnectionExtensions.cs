using SQLite;

namespace Sefirah.Data.AppDatabase;

public static class SQLiteConnectionExtensions
{
    /// <summary>
    /// Inserts a row, or updates the existing row on primary-key conflict.
    /// Unlike <see cref="SQLiteConnection.InsertOrReplace"/>, this preserves <c>rowid</c>.
    /// Uses the same mapping/column path as <see cref="SQLiteConnection.InsertOrReplace"/>.
    /// </summary>
    public static int InsertOrUpdate(this SQLiteConnection connection, object obj)
    {
        if (obj is null)
            return 0;

        return InsertOrUpdate(connection, obj, Orm.GetType(obj));
    }

    public static int InsertOrUpdate(this SQLiteConnection connection, object obj, Type objType)
    {
        if (obj is null || objType is null)
            return 0;

        var map = connection.GetMapping(objType);
        if (map.PK is null)
            throw new InvalidOperationException($"Type {map.MappedType.Name} has no primary key.");

        var cols = map.InsertOrReplaceColumns;
        var columnList = string.Join(", ", cols.Select(c => $"\"{c.Name}\""));
        var placeholders = string.Join(", ", cols.Select(_ => "?"));
        var updates = string.Join(
            ", ",
            cols.Where(c => !c.IsPK).Select(c => $"\"{c.Name}\"=excluded.\"{c.Name}\""));

        var sql = $"""
            INSERT INTO "{map.TableName}" ({columnList})
            VALUES ({placeholders})
            ON CONFLICT("{map.PK.Name}") DO UPDATE SET
                {updates}
            """;

        var args = cols.Select(c => c.GetValue(obj)).ToArray();
        return connection.Execute(sql, args);
    }

    /// <summary>
    /// Resolves the internal <c>rowid</c> for a row identified by its primary-key column value.
    /// </summary>
    public static long GetRowId(this SQLiteConnection connection, string table, string keyColumn, object keyValue) =>
        connection.ExecuteScalar<long>(
            $"SELECT rowid FROM \"{table}\" WHERE \"{keyColumn}\" = ?",
            keyValue);

    /// <summary>
    /// Resolves the internal <c>rowid</c> for an entity using its mapped primary key.
    /// </summary>
    public static long GetRowId(this SQLiteConnection connection, object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var map = connection.GetMapping(Orm.GetType(entity));
        if (map.PK is null)
            throw new InvalidOperationException($"Type {map.MappedType.Name} has no primary key.");

        return connection.GetRowId(map.TableName, map.PK.Name, map.PK.GetValue(entity)!);
    }
}
