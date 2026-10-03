using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IFileTransferService
{
    Task<StorageFile?> Receive(FileTransferSession session, PairedDevice device, bool silent = false);

    Task Send(StorageFile[] files, PairedDevice device, bool silent = false);

    void SendFilesWithPicker(IReadOnlyList<IStorageItem> storageItems);
    void CancelAllTransfers();
    void CancelTransfer(Guid guid);
}
