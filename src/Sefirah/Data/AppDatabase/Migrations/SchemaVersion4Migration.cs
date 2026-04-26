using SQLite;

namespace Sefirah.Data.AppDatabase.Migrations;

/// <summary>
/// Adds calling Bluetooth columns
/// </summary>
public sealed class SchemaVersion4Migration : IMigration
{
    public int TargetVersion => 4;

    public void Up(SQLiteConnection db)
    {
        var cols = db.GetTableInfo("PairedDeviceEntity");
        if (!cols.Exists(c => c.Name == "CallsTransportDeviceId"))
        {
            db.Execute("ALTER TABLE PairedDeviceEntity ADD COLUMN CallsTransportDeviceId TEXT;");
        }

        db.Execute("ALTER TABLE PairedDeviceEntity ADD COLUMN BluetoothAddress TEXT;");

        db.Execute("ALTER TABLE PairedDeviceEntity ADD COLUMN BluetoothClassicDeviceId TEXT;");
    }
}
