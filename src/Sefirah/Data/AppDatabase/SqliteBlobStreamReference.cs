using SQLite;
using SQLitePCL;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Sefirah.Data.AppDatabase;

public sealed partial class SqliteBlobStreamReference(SQLiteConnection connection, string table, string column, long rowId) : IRandomAccessStreamReference
{
    public IAsyncOperation<IRandomAccessStreamWithContentType> OpenReadAsync() =>
        OpenReadInternalAsync().AsAsyncOperation();

    private async Task<IRandomAccessStreamWithContentType> OpenReadInternalAsync()
    {
        var bytes = await Task.Run(ReadBlob).ConfigureAwait(false);
        return new MemoryStream(bytes).AsRandomAccessStreamWithContentType(string.Empty);
    }

    private byte[] ReadBlob()
    {
        if (rowId <= 0)
            return [];

        var rc = raw.sqlite3_blob_open(connection.Handle, "main", table, column, rowId, 0, out var blob);
        if (rc != raw.SQLITE_OK)
            return [];

        try
        {
            int size = raw.sqlite3_blob_bytes(blob);
            if (size <= 0)
                return [];

            var bytes = new byte[size];
            rc = raw.sqlite3_blob_read(blob, bytes, 0);
            return rc == raw.SQLITE_OK ? bytes : [];
        }
        finally
        {
            raw.sqlite3_blob_close(blob);
        }
    }
}
