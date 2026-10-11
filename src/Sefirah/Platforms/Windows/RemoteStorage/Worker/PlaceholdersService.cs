using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using Vanara.PInvoke;
using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;

public class PlaceholdersService(
    ISyncProviderContextAccessor contextAccessor,
    IRemoteReadWriteService remoteService,
    FileLocker fileLocker,
    ILogger logger
)
{
    private string RootDirectory => contextAccessor.Context.RootDirectory;
    private readonly FileEqualityComparer _fileComparer = new();

    /// <summary>Recursively creates placeholders for all remote files and directories under <paramref name="subpath"/>.</summary>
    public void CreateBulk(string subpath)
    {
        using (var safeFilePlaceholderCreateInfos = remoteService.EnumerateFiles(subpath)
            .Where((x) => !FileHelper.IsSystemDirectory(x.RelativePath))
            .Select(GetFilePlaceholderCreateInfo)
            .ToDisposableArray()
        )
        {
            // Create one at a time; prone to errors when done with list
            foreach (var createInfo in safeFilePlaceholderCreateInfos.Source)
            {
                CldApi.CfCreatePlaceholders(
                    Path.Join(RootDirectory, subpath),
                    [createInfo],
                    1,
                    CldApi.CF_CREATE_FLAGS.CF_CREATE_FLAG_NONE,
                    out var fileEntriesProcessed
                ).ThrowIfFailed($"Create file placeholder failed");
            }
        }

        var remoteSubDirectories = remoteService.EnumerateDirectories(subpath);
        using (var safeDirectoryPlaceholderCreateInfos = remoteSubDirectories
            .Select(GetDirectoryPlaceholderCreateInfo)
            .ToDisposableArray()
        )
        {
            // Create one at a time; prone to errors when done with list
            foreach (var createInfo in safeDirectoryPlaceholderCreateInfos.Source)
            {
                CldApi.CfCreatePlaceholders(
                    Path.Join(RootDirectory, subpath),
                    [createInfo],
                    1,
                    CldApi.CF_CREATE_FLAGS.CF_CREATE_FLAG_NONE,
                    out var directoryEntriesProcessed
                ).ThrowIfFailed("Create directory placeholders failed");
            }
        }

        foreach (var remoteSubDirectory in remoteSubDirectories)
        {
            CreateBulk(remoteSubDirectory.RelativePath);
        }
    }

    public Task CreateOrUpdateFile(string relativeFile)
    {
        var clientFile = Path.Join(RootDirectory, relativeFile);
        return !File.Exists(clientFile)
            ? CreateFile(relativeFile)
            : UpdateFile(relativeFile);
    }

    public Task CreateOrUpdateDirectory(string relativeDirectory)
    {
        var clientDirectory = Path.Join(RootDirectory, relativeDirectory);
        return !Directory.Exists(clientDirectory)
            ? CreateDirectory(relativeDirectory)
            : UpdateDirectory(relativeDirectory);
    }

    public Task CreateFile(string relativeFile)
    {
        var fileInfo = remoteService.GetFileInfo(relativeFile);
        var parentPath = Path.Join(RootDirectory, fileInfo.RelativeParentDirectory);

        using var createInfo = new SafeCreateInfo(fileInfo, fileInfo.RelativePath);
        CldApi.CfCreatePlaceholders(
            parentPath,
            [createInfo],
            1u,
            CldApi.CF_CREATE_FLAGS.CF_CREATE_FLAG_NONE,
            out var entriesProcessed
        ).ThrowIfFailed($"Create placeholder failed: {relativeFile}");

        logger.Info($"Created placeholder file for {relativeFile}");
        return Task.CompletedTask;
    }

    public async Task CreateDirectory(string relativeDirectory)
    {

        var directoryInfo = remoteService.GetDirectoryInfo(relativeDirectory);
        var parentPath = Path.Join(RootDirectory, directoryInfo.RelativeParentDirectory);
        var targetPath = Path.Join(RootDirectory, relativeDirectory);

        // If it's already a placeholder, just update it
        if (CloudFilter.IsPlaceholder(targetPath))
        {
            logger.Info($"Directory is already a placeholder, updating {relativeDirectory}");
            await UpdateDirectory(relativeDirectory);
            return;
        }

        try
        {
            // Attempt to create the placeholder
            using var createInfo = new SafeCreateInfo(directoryInfo, directoryInfo.RelativePath, onDemand: false);
            CldApi.CfCreatePlaceholders(
                parentPath,
                [createInfo],
                1u,
                CldApi.CF_CREATE_FLAGS.CF_CREATE_FLAG_NONE,
                out var entriesProcessed
            ).ThrowIfFailed($"Create placeholder failed: {relativeDirectory}");

            logger.Info($"Created placeholder for {relativeDirectory}");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to create placeholder for {relativeDirectory}: {ex.Message}", ex);
            throw;
        }
    }


    private SafeCreateInfo GetFilePlaceholderCreateInfo(RemoteFileInfo remoteFileInfo) =>
        new(remoteFileInfo, remoteFileInfo.RelativePath);

    private SafeCreateInfo GetDirectoryPlaceholderCreateInfo(RemoteDirectoryInfo remoteDirectoryInfo) =>
        new(remoteDirectoryInfo, remoteDirectoryInfo.RelativePath, onDemand: false);

    /// <summary>Syncs a file with the remote; If it's newer on the remote, it's downloaded. Otherwise, it's uploaded.</summary>
    public async Task UpdateFile(string relativeFile)
    {
        var clientFile = Path.Join(RootDirectory, relativeFile);
        if (!Path.Exists(clientFile))
        {
            //logger.Info("Skip update; file does not exist {clientFile}", clientFile);
            return;
        }
        var clientFileInfo = new FileInfo(clientFile);
        var downloaded = !clientFileInfo.Attributes.HasFlag(FileAttributes.Offline);

        // Upload before the exclusive handle is opened below
        if (downloaded && await TryUploadChanges(relativeFile, clientFileInfo))
        {
            return;
        }

        if (clientFileInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            clientFileInfo.Attributes &= ~FileAttributes.ReadOnly;
        }

        using var hfile = downloaded
            ? await FileHelper.WaitUntilUnlocked(() => CloudFilter.CreateHFileWithOplock(clientFile, FileAccess.Write), logger)
            : CloudFilter.CreateHFile(clientFile, FileAccess.Write);
        var placeholderState = CloudFilter.GetPlaceholderState(hfile);

        if (!placeholderState.HasFlag(CldApi.CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PLACEHOLDER))
        {
            try
            {
                CloudFilter.ConvertToPlaceholder(hfile, relativeFile);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to convert to placeholder for {relativeFile}", ex);
                return;
            }
        }

        var remoteFileInfo = remoteService.GetFileInfo(relativeFile);

        // Local matches remote, just make sure it's flagged in-sync.
        if (MatchesRemote(clientFileInfo, remoteFileInfo))
        {
            if (!placeholderState.HasFlag(CldApi.CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC))
            {
                CloudFilter.SetInSyncState(hfile);
            }
            return;
        }

        var pinned = clientFileInfo.Attributes.IsPinned();
        if (pinned)
        {
            // Clear Pinned to avoid 392 ERROR_CLOUD_FILE_PINNED
            CloudFilter.SetPinnedState(hfile, 0);
        }
        var redownload = downloaded && !clientFileInfo.Attributes.HasFlag(SyncAttributes.Unpinned);
        var relativePath = PathMapper.GetRelativePath(clientFile, RootDirectory);
        var usn = downloaded
            ? CloudFilter.UpdateAndDehydratePlaceholder(hfile, relativePath, remoteFileInfo)
            : CloudFilter.UpdateFilePlaceholder(hfile, relativePath, remoteFileInfo);
        if (pinned)
        {
            // ClientWatcher calls HydratePlaceholder when both Offline and Pinned are set
            CloudFilter.SetPinnedState(hfile, SyncAttributes.Pinned);
        }
        else if (redownload)
        {
            CloudFilter.HydratePlaceholder(hfile);
        }
    }

    /// <returns>False when there are no local changes or the remote is newer.</returns>
    private async Task<bool> TryUploadChanges(string relativeFile, FileInfo clientFileInfo)
    {
        var clientFile = clientFileInfo.FullName;
        if (!CloudFilter.HasLocalChanges(clientFile))
        {
            return false;
        }

        if (!remoteService.Exists(relativeFile))
        {
            await remoteService.CreateFile(clientFileInfo, relativeFile);
        }
        else
        {
            var remoteFileInfo = remoteService.GetFileInfo(relativeFile);
            if (!MatchesRemote(clientFileInfo, remoteFileInfo))
            {
                var localWriteTime = FileEqualityComparer.NormalizeToUtcSeconds(clientFileInfo.LastWriteTimeUtc);
                var remoteWriteTime = FileEqualityComparer.NormalizeToUtcSeconds(remoteFileInfo.LastWriteTimeUtc);
                if (localWriteTime < remoteWriteTime)
                {
                    logger.Info($"Remote copy of {relativeFile} is newer, replacing local changes");
                    return false;
                }
                await remoteService.UpdateFile(clientFileInfo, relativeFile);
            }
        }

        CloudFilter.MarkInSync(clientFile, relativeFile);
        ShellNotify.NotifyUpdate(clientFile);
        return true;
    }

    private bool MatchesRemote(FileInfo clientFileInfo, RemoteFileInfo remoteFileInfo) =>
        remoteFileInfo.GetHashCode() == _fileComparer.GetHashCode(clientFileInfo);

    /// <summary>Converts a hydrated non-placeholder directory to a placeholder and marks it in-sync. No-ops on dehydrated dirs.</summary>
    public Task UpdateDirectory(string relativeDirectory)
    {
        var clientDirectory = Path.Join(RootDirectory, relativeDirectory);
        if (!Path.Exists(clientDirectory))
        {
            //logger.Warn("Skip update; directory does not exist {clientDirectory}", clientDirectory);
            return Task.CompletedTask;
        }

        if (!File.GetAttributes(clientDirectory).HasFlag(FileAttributes.Offline))
        {
            try
            {
                CloudFilter.MarkInSync(clientDirectory, relativeDirectory);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to mark directory in-sync for {relativeDirectory}", ex);
            }
        }
        return Task.CompletedTask;
    }

    public async Task RenameFile(string oldRelativeFile, string newRelativeFile)
    {
        var oldClientFile = Path.Join(RootDirectory, oldRelativeFile);
        if (!Path.Exists(oldClientFile))
        {
            await CreateOrUpdateFile(newRelativeFile);
            return;
        }
        var newClientFile = Path.Join(RootDirectory, newRelativeFile);
        File.Move(oldClientFile, newClientFile);

        CloudFilter.SetInSyncState(newClientFile);
    }

    public async Task RenameDirectory(string oldRelativePath, string newRelativePath)
    {
        var oldClientDirectory = Path.Join(RootDirectory, oldRelativePath);
        if (!Path.Exists(oldClientDirectory))
        {
            await CreateOrUpdateDirectory(newRelativePath);
            return;
        }
        var newClientDirectory = Path.Join(RootDirectory, newRelativePath);
        Directory.Move(oldClientDirectory, newClientDirectory);

        CloudFilter.SetInSyncState(newClientDirectory);
    }

    public void DeleteBulk(string relativeDirectory)
    {
        var clientDirectory = Path.Join(RootDirectory, relativeDirectory);
        var entries = Directory.EnumerateDirectories(clientDirectory)
            .Concat(Directory.EnumerateFiles(clientDirectory))
            .ToArray();

        if (entries.Length == 0)
        {
            return;
        }

        try
        {
            RecycleBin.MoveToRecycleBin(entries);
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to move items under {clientDirectory} to Recycle Bin, deleting permanently", ex);
            foreach (var clientSubDirectory in Directory.EnumerateDirectories(clientDirectory))
            {
                Directory.Delete(clientSubDirectory, recursive: true);
            }
            foreach (var clientFile in Directory.EnumerateFiles(clientDirectory))
            {
                File.Delete(clientFile);
            }
        }
    }

    /// <summary>Hydrates a file, or every file under a folder. Callers hold the lock.</summary>
    public void Hydrate(string relativePath)
    {
        var clientPath = Path.Join(RootDirectory, relativePath);
        var attributes = File.GetAttributes(clientPath);
        if (!attributes.HasFlag(FileAttributes.Directory))
        {
            if (attributes.HasFlag(FileAttributes.Offline) && !FileHelper.IsSystemFile(clientPath))
            {
                CloudFilter.HydratePlaceholder(clientPath);
            }
            return;
        }

        // Enumerating fetches the placeholders of an unpopulated folder
        foreach (var entry in Directory.EnumerateFileSystemEntries(clientPath).ToArray())
        {
            var entryRelativePath = PathMapper.GetRelativePath(entry, RootDirectory);
            try
            {
                Hydrate(entryRelativePath);
            }
            catch (Exception ex)
            {
                logger.Error($"Hydrate failed for {entryRelativePath}", ex);
            }
        }
    }

    /// <summary>Moves the item to the recycle bin, or re-uploads it if it has local changes.</summary>
    public async Task Delete(string relativePath)
    {
        var clientPath = Path.Join(RootDirectory, relativePath);
        if (!Path.Exists(clientPath))
        {
            return;
        }
        var attributes = File.GetAttributes(clientPath);
        var isDirectory = attributes.HasFlag(FileAttributes.Directory);

        if (!isDirectory && !attributes.HasFlag(FileAttributes.Offline) && CloudFilter.HasLocalChanges(clientPath))
        {
            await UpdateFile(relativePath);
            return;
        }

        try
        {
            RecycleBin.MoveToRecycleBin(clientPath);
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to move {clientPath} to Recycle Bin, deleting permanently", ex);
            if (isDirectory)
            {
                Directory.Delete(clientPath, recursive: true);
            }
            else
            {
                File.Delete(clientPath);
            }
            ShellNotify.NotifyDelete(clientPath, isDirectory);
        }
    }

    /// <summary>
    /// Applies local renames, uploads local changes under <paramref name="relativeDirectory"/>
    /// and removes in-sync items that are gone from the remote. Takes its own file locks.
    /// </summary>
    /// <param name="movedDirectories">Folders moved on the remote during this walk, as (old, new) paths.</param>
    public async Task SyncPendingChanges(string relativeDirectory, List<(string Old, string New)>? movedDirectories = null)
    {
        movedDirectories ??= [];
        var directoryInfo = new DirectoryInfo(Path.Join(RootDirectory, relativeDirectory));
        // Online-only folders have no local contents
        if (!directoryInfo.Exists || directoryInfo.Attributes.HasFlag(FileAttributes.Offline))
        {
            return;
        }

        // Folder created locally that isn't on the remote yet
        if (relativeDirectory.Length > 0
            && CloudFilter.HasLocalChanges(directoryInfo.FullName)
            && !remoteService.Exists(relativeDirectory))
        {
            await remoteService.CreateDirectory(directoryInfo, relativeDirectory);
        }

        // Names in the remote folder, listed once for the whole loop
        var remoteNames = remoteService.EnumerateFiles(relativeDirectory).Select(x => x.Name)
            .Concat(remoteService.EnumerateDirectories(relativeDirectory).Select(x => x.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // An empty listing from a dropped connection would look like everything was deleted
        var canRemove = remoteService.IsConnected;

        foreach (var entry in directoryInfo.EnumerateFileSystemInfos().ToArray())
        {
            var relativePath = PathMapper.GetRelativePath(entry.FullName, RootDirectory);
            try
            {
                if (FileHelper.IsSystemFile(entry.FullName))
                {
                    continue;
                }

                // Renamed or moved locally while disconnected; the listing above still has the old name
                if (await TrySyncRename(entry, relativePath, movedDirectories))
                {
                    remoteNames.Add(entry.Name);
                }

                var state = CloudFilter.GetPlaceholderState(entry.FullName);
                if (state is CldApi.CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_INVALID)
                {
                    continue;
                }

                // In sync but gone from the remote: deleted there
                if (!CloudFilter.HasLocalChanges(state) && !remoteNames.Contains(entry.Name))
                {
                    if (canRemove)
                    {
                        using var locker = await fileLocker.Lock(relativePath);
                        await Delete(relativePath);
                    }
                    continue;
                }

                if (entry is DirectoryInfo)
                {
                    await SyncPendingChanges(relativePath, movedDirectories);
                }
                // Downloaded file with local changes: upload it, or take the remote copy if that's newer
                else if (CloudFilter.HasLocalChanges(state) && !entry.Attributes.HasFlag(FileAttributes.Offline))
                {
                    using var locker = await fileLocker.Lock(relativePath);
                    await UpdateFile(relativePath);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to sync pending change {relativePath}", ex);
            }
        }
    }

    /// <summary>
    /// Renames the item on the remote if its placeholder identity (the path it was synced at) differs from where it is now.
    /// </summary>
    /// <returns>True if the item was renamed on the remote.</returns>
    private async Task<bool> TrySyncRename(FileSystemInfo entry, string relativePath, List<(string Old, string New)> movedDirectories)
    {
        // Path the item was last synced at; null if it isn't a placeholder
        var identity = CloudFilter.GetPlaceholderIdentity(entry.FullName);
        if (identity is null || identity == relativePath)
        {
            return false;
        }

        // Where the item is on the remote now, if a parent folder was already moved there in this walk
        var oldPath = movedDirectories.Aggregate(identity, (path, moved) =>
            path.StartsWith(moved.Old + "/") ? moved.New + path[moved.Old.Length..] : path);

        // Rename only if the old path is gone locally, still on the remote, and the new one isn't taken there
        var renamed = oldPath != relativePath
            && !Path.Exists(Path.Join(RootDirectory, oldPath))
            && remoteService.Exists(oldPath)
            && !remoteService.Exists(relativePath);
        if (renamed)
        {
            using var oldLocker = await fileLocker.Lock(oldPath);
            using var newLocker = await fileLocker.Lock(relativePath);
            if (entry is DirectoryInfo)
            {
                remoteService.MoveDirectory(oldPath, relativePath);
                // Lets children inside it be found at their new remote path
                movedDirectories.Add((oldPath, relativePath));
            }
            else
            {
                remoteService.MoveFile(oldPath, relativePath);
            }
            // Downloaded files are marked in sync by UpdateFile once checked against the remote
            if (entry is DirectoryInfo || entry.Attributes.HasFlag(FileAttributes.Offline))
            {
                CloudFilter.SetInSyncState(entry.FullName);
            }
            logger.Info($"Renamed {oldPath} to {relativePath} on the remote");
        }

        // Renamed above, moved along with a folder, or stale
        CloudFilter.SetPlaceholderIdentity(entry.FullName, relativePath);
        return renamed;
    }
}
