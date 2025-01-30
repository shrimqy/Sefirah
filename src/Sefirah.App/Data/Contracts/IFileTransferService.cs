using Sefirah.App.Data.Models;
using System.IO;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;

namespace Sefirah.App.Data.Contracts;
public interface IFileTransferService
{
    /// <summary>
    /// Initializes the file receiver with remote server information and the file metadata.
    /// </summary>
    Task ReceiveFile(FileTransfer data);

    /// <summary>
    /// Receives a bulk of files.
    /// </summary>
    Task ReceiveBulkFiles(BulkFileTransfer data);

    /// <summary>
    /// Processes the share operation to send a file to the remote device.
    /// </summary>
    Task ProcessShareAsync(ShareOperation data);

    /// <summary>
    /// Sends a file to the remote device (Used for clipboard image transfer)
    /// </summary>
    Task SendFile(Stream stream, FileMetadata metadata);

    event EventHandler<StorageFile> FileReceived;
}
