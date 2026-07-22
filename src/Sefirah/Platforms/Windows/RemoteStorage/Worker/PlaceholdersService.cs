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

    /// <summary>
    /// Syncs local placeholder metadata with the remote. Skips if hashes match unless <paramref name="force"/> is set.
    /// If the file was downloaded, dehydrates it after updating.
    /// </summary>
    public async Task UpdateFile(string relativeFile, bool force = false)
    {
        var clientFile = Path.Join(RootDirectory, relativeFile);
        if (!Path.Exists(clientFile))
        {
            //logger.Info("Skip update; file does not exist {clientFile}", clientFile);
            return;
        }
        var clientFileInfo = new FileInfo(clientFile);
        var downloaded = !clientFileInfo.Attributes.HasFlag(FileAttributes.Offline);
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
                CloudFilter.ConvertToPlaceholder(hfile);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to convert to placeholder for {relativeFile}", ex);
                return;
            }
        }

        // Skip if placeholder isn't synced yet (Possibly a local edit and not a remote change)
        if (!force && downloaded && !placeholderState.HasFlag(CldApi.CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC))
        {
            return;
        }

        var remoteFileInfo = remoteService.GetFileInfo(relativeFile);

        if (!force && remoteFileInfo.GetHashCode() == _fileComparer.GetHashCode(clientFileInfo))
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

    /// <summary>Converts a hydrated non-placeholder directory to a placeholder and marks it in-sync. No-ops on dehydrated dirs.</summary>
    public Task UpdateDirectory(string relativeDirectory)
    {
        var clientDirectory = Path.Join(RootDirectory, relativeDirectory);
        if (!Path.Exists(clientDirectory))
        {
            //logger.Warn("Skip update; directory does not exist {clientDirectory}", clientDirectory);
            return Task.CompletedTask;
        }

        // Check if the directory is hydrated
        bool isHydrated = !File.GetAttributes(clientDirectory).HasFlag(FileAttributes.Offline);

        // Only update placeholder state if directory is a hydrated directory
        if (isHydrated)
        {
            if (!CloudFilter.IsPlaceholder(clientDirectory))
            {
                try
                {
                    CloudFilter.ConvertToPlaceholder(clientDirectory);
                    CloudFilter.SetInSyncState(clientDirectory);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to convert directory to placeholder for {relativeDirectory}", ex);
                }
                return Task.CompletedTask;
            }
            CloudFilter.SetInSyncState(clientDirectory);
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

    // for now move the file to the recycle bin
    public void Delete(string relativePath)
    {
        var clientPath = Path.Join(RootDirectory, relativePath);
        if (!Path.Exists(clientPath))
        {
            return;
        }
        var isDirectory = File.GetAttributes(clientPath).HasFlag(FileAttributes.Directory);
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
}
