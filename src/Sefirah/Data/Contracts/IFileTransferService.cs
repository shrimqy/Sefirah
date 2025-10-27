using Sefirah.Data.Enums;
using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;
public interface IFileTransferService
{
    /// <summary>
    /// Initializes the file receiver with remote server information and the file metadata.
    /// </summary>
    Task ReceiveFile(FileTransfer data, PairedDevice device);

    /// <summary>
    /// Receives a bulk of files.
    /// </summary>
    Task ReceiveBulkFiles(BulkFileTransfer data, PairedDevice device);

    Task SendFile(StorageFile file, FileMetadata metadata, PairedDevice device, FileTransferType transferType);

    /// <summary>
    /// Sends a file to the remote device (Used for clipboard image transfer)
    /// </summary>
    Task SendBulkFiles(StorageFile[] files, PairedDevice device);

    event EventHandler<(PairedDevice device, StorageFile data)> FileReceived;

    void SendFiles(IReadOnlyList<IStorageItem> storageItems);
    void CancelTransfer();
}
