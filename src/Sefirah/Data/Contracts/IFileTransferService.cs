using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IFileTransferService
{
    /// <summary>
    /// Receives files from a remote device.
    /// </summary>
    Task ReceiveFiles(FileTransferInfo data, PairedDevice device);

    /// <summary>
    /// Sends files to a remote device.
    /// </summary>
    Task SendFiles(StorageFile[] files, PairedDevice device, bool isClipboard = false);

    event EventHandler<(PairedDevice device, StorageFile data)> FileReceived;

    void SendFilesWithPicker(IReadOnlyList<IStorageItem> storageItems);
    void CancelAllTransfers();
    void CancelTransfer(Guid guid);
}
