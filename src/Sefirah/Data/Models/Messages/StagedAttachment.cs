using Sefirah.Helpers;

namespace Sefirah.Data.Models.Messages;

public partial class StagedAttachment : AttachmentBase
{
    private readonly ImageSource? previewImage;

    public required IStorageFile File { get; init; }

    public override bool HasPreview => previewImage is not null;

    public override ImageSource? PreviewImage => previewImage;

    private StagedAttachment(ImageSource? previewImage)
    {
        this.previewImage = previewImage;
    }

    public static async Task<StagedAttachment> CreateAsync(StorageFile file, string mimeType)
    {
        ImageSource? preview = null;
        if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            preview = await StorageFileHelper.ToBitmapAsync(file);

        return new StagedAttachment(preview)
        {
            File = file,
            FileName = file.Name,
            MimeType = mimeType
        };
    }
}
