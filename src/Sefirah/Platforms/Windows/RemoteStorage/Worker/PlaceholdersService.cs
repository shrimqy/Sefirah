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
    private string rootDirectory => contextAccessor.Context.RootDirectory;
    private readonly FileEqualityComparer _fileComparer = new();
    private readonly DirectoryEqualityComparer _directoryComparer = new();

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
                    Path.Join(rootDirectory, subpath),
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
                    Path.Join(rootDirectory, subpath),
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
        var clientFile = Path.Join(rootDirectory, relativeFile);
        return !File.Exists(clientFile)
            ? CreateFile(relativeFile)
            : UpdateFile(relativeFile);
    }

    public Task CreateOrUpdateDirectory(string relativeDirectory)
    {
        var clientDirectory = Path.Join(rootDirectory, relativeDirectory);
        return !Directory.Exists(clientDirectory)
            ? CreateDirectory(relativeDirectory)
            : UpdateDirectory(relativeDirectory);
    }

    public async Task CreateFile(string relativeFile)
    {
        var fileInfo = remoteService.GetFileInfo(relativeFile);
        var parentPath = Path.Join(rootDirectory, fileInfo.RelativeParentDirectory);

        using var createInfo = new SafeCreateInfo(fileInfo, fileInfo.RelativePath);
        CldApi.CfCreatePlaceholders(
            parentPath,
            [createInfo],
            1u,
            CldApi.CF_CREATE_FLAGS.CF_CREATE_FLAG_NONE,
            out var entriesProcessed
        ).ThrowIfFailed("Create placeholder failed");

        logger.LogInformation("Created placeholder file for {path}", relativeFile);
    }

    public async Task CreateDirectory(string relativeDirectory)
    {

        var directoryInfo = remoteService.GetDirectoryInfo(relativeDirectory);
        var parentPath = Path.Join(rootDirectory, directoryInfo.RelativeParentDirectory);
        var targetPath = Path.Join(rootDirectory, relativeDirectory);

        // If it's already a placeholder, just update it
        if (CloudFilter.IsPlaceholder(targetPath))
        {
            logger.LogInformation("Directory is already a placeholder, updating {path}", relativeDirectory);
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
            ).ThrowIfFailed("Create placeholder failed");

            logger.LogInformation("Created placeholder for {path}", relativeDirectory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create placeholder for {path}: {error}", relativeDirectory, ex.Message);
            throw;
        }
    }


    private SafeCreateInfo GetFilePlaceholderCreateInfo(RemoteFileInfo remoteFileInfo) =>
        new(remoteFileInfo, remoteFileInfo.RelativePath);

    private SafeCreateInfo GetDirectoryPlaceholderCreateInfo(RemoteDirectoryInfo remoteDirectoryInfo) =>
        new(remoteDirectoryInfo, remoteDirectoryInfo.RelativePath, onDemand: false);

    public async Task UpdateFile(string relativeFile, bool force = false)
    {
        var clientFile = Path.Join(rootDirectory, relativeFile);
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
            CloudFilter.ConvertToPlaceholder(hfile);
        }

        var remoteFileInfo = remoteService.GetFileInfo(relativeFile);
        if (!force && remoteFileInfo.GetHashCode() == _fileComparer.GetHashCode(clientFileInfo))
        {
            //logger.Info("UpdateFile - equal, ignoring {relativeFile}", relativeFile);
            if (!placeholderState.HasFlag(CldApi.CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC))
            {
                CloudFilter.SetInSyncState(hfile);
            }
            return;
        }

        logger.LogInformation("UpdateFile - update placeholder {relativeFile}", relativeFile);
        var pinned = clientFileInfo.Attributes.HasAnySyncFlag(SyncAttributes.PINNED);
        if (pinned)
        {
            // Clear Pinned to avoid 392 ERROR_CLOUD_FILE_PINNED
            CloudFilter.SetPinnedState(hfile, 0);
        }
        var redownload = downloaded && !clientFileInfo.Attributes.HasAnySyncFlag(SyncAttributes.UNPINNED);
        var relativePath = PathMapper.GetRelativePath(clientFile, rootDirectory);
        var usn = downloaded
            ? CloudFilter.UpdateAndDehydratePlaceholder(hfile, relativePath, remoteFileInfo)
            : CloudFilter.UpdateFilePlaceholder(hfile, relativePath, remoteFileInfo);
        if (pinned)
        {
            // ClientWatcher calls HydratePlaceholder when both Offline and Pinned are set
            CloudFilter.SetPinnedState(hfile, SyncAttributes.PINNED);
        }
        else if (redownload)
        {
            CloudFilter.HydratePlaceholder(hfile);
        }
    }

    public Task UpdateDirectory(string relativeDirectory)
    {
        var clientDirectory = Path.Join(rootDirectory, relativeDirectory);
        if (!Path.Exists(clientDirectory))
        {
            //logger.Warn("Skip update; directory does not exist {clientDirectory}", clientDirectory);
            return Task.CompletedTask;
        }

        var remoteDirectoryInfo = remoteService.GetDirectoryInfo(relativeDirectory);

        // Check if the directory is hydrated 
        bool isHydrated = !File.GetAttributes(clientDirectory).HasAnySyncFlag(SyncAttributes.OFFLINE);
        
        // Only update placeholder state if directory is a hydrated directory
        if (isHydrated)
        {
            logger.LogInformation("Directory {path} is hydrated", relativeDirectory);
            if (!CloudFilter.IsPlaceholder(clientDirectory))
            {
                CloudFilter.ConvertToPlaceholder(clientDirectory);
                CloudFilter.SetInSyncState(clientDirectory);
                logger.LogInformation("Converted to placeholder and updated {path}", relativeDirectory);
                return Task.CompletedTask;
            }
            CloudFilter.SetInSyncState(clientDirectory);
        }
        return Task.CompletedTask;
    }

    public async Task RenameFile(string oldRelativeFile, string newRelativeFile)
    {
        var oldClientFile = Path.Join(rootDirectory, oldRelativeFile);
        if (!Path.Exists(oldClientFile))
        {
            await CreateOrUpdateFile(newRelativeFile);
            return;
        }
        var newClientFile = Path.Join(rootDirectory, newRelativeFile);
        File.Move(oldClientFile, newClientFile);

        CloudFilter.SetInSyncState(newClientFile);
    }

    public async Task RenameDirectory(string oldRelativePath, string newRelativePath)
    {
        var oldClientDirectory = Path.Join(rootDirectory, oldRelativePath);
        if (!Path.Exists(oldClientDirectory))
        {
            await CreateOrUpdateDirectory(newRelativePath);
            return;
        }
        var newClientDirectory = Path.Join(rootDirectory, newRelativePath);
        Directory.Move(oldClientDirectory, newClientDirectory);

        CloudFilter.SetInSyncState(newClientDirectory);
    }

    public void DeleteBulk(string relativeDirectory)
    {
        var clientDirectory = Path.Join(rootDirectory, relativeDirectory);
        var clientSubDirectories = Directory.EnumerateDirectories(clientDirectory);
        foreach (var clientSubDirectory in clientSubDirectories)
        {
            Directory.Delete(clientSubDirectory, recursive: true);
        }

        var clientFiles = Directory.EnumerateFiles(clientDirectory);
        foreach (var clientFile in clientFiles)
        {
            File.Delete(clientFile);
        }
    }

    public void Delete(string relativePath)
    {
        var clientPath = Path.Join(rootDirectory, relativePath);
        if (!Path.Exists(clientPath))
        {
            return;
        }
        if (File.GetAttributes(clientPath).HasFlag(FileAttributes.Directory))
        {
            Directory.Delete(clientPath, recursive: true);
        }
        else
        {
            File.Delete(clientPath);
        }
    }
}
