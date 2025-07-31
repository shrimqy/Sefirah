namespace Sefirah.Platforms.Windows.Interop.SyncRoot;
public record SyncRootEvents
{
    public SyncRootCallback? FetchData { get; init; }
    public SyncRootCallback? CancelFetchData { get; init; }
    public SyncRootCallback? FetchPlaceholders { get; init; }
    public SyncRootCallback? CancelFetchPlaceholders { get; init; }
    public SyncRootCallback? OnOpenCompletion { get; init; }
    public SyncRootCallback? OnCloseCompletion { get; init; }
    public SyncRootCallback? OnRename { get; init; }
    public SyncRootCallback? OnRenameCompletion { get; init; }
    public SyncRootCallback? OnDelete { get; init; }
    public SyncRootCallback? OnDeleteCompletion { get; init; }
}
