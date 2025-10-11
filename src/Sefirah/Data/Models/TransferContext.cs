namespace Sefirah.Data.Models;

public class TransferContext(string device, string transferId, List<FileMetadata> files)
{
    public List<FileMetadata> Files { get; set; } = files;
    public string TransferId { get; set; } = transferId;
    public string Device { get; set; } = device;
    public long BytesTransferred { get; set; } = 0;
    public long TotalBytes { get; set; } = files.Sum(f => f.FileSize);
    public int CurrentFileIndex { get; set; } = 0;
}
