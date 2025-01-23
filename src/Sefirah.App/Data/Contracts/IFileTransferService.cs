using Sefirah.App.Data.Models;
using System.IO;
using Windows.ApplicationModel.DataTransfer.ShareTarget;

namespace Sefirah.App.Data.Contracts;
public interface IFileTransferService
{
    /// <summary>
    /// To initialize the file transfer notifications.
    /// </summary>
    Task InitializeAsync();

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
}
