using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;

namespace Sefirah.Platforms.Windows.Interop;
public sealed class SafeCreateInfo : IDisposable
{
    private readonly SafeCoTaskMemString _relativePathPointer;
    public CldApi.CF_PLACEHOLDER_CREATE_INFO CreateInfo { get; init; }

    public SafeCreateInfo(RemoteFileInfo serverFileInfo, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            throw new ArgumentException("Relative path cannot be null or empty", nameof(relativePath));
        }
        
        try
        {
            _relativePathPointer = new SafeCoTaskMemString(relativePath);
            CreateInfo = new CldApi.CF_PLACEHOLDER_CREATE_INFO
            {
                RelativeFileName = serverFileInfo.Name,
                FileIdentity = _relativePathPointer,
                FileIdentityLength = _relativePathPointer.Size,
                FsMetadata = new CldApi.CF_FS_METADATA
                {
                    FileSize = serverFileInfo.Length,
                    BasicInfo = new Kernel32.FILE_BASIC_INFO
                    {
                        FileAttributes = (FileFlagsAndAttributes)serverFileInfo.Attributes,
                        CreationTime = serverFileInfo.CreationTimeUtc.ToFileTimeStruct(),
                        LastWriteTime = serverFileInfo.LastWriteTimeUtc.ToFileTimeStruct(),
                        LastAccessTime = serverFileInfo.LastAccessTimeUtc.ToFileTimeStruct(),
                        ChangeTime = serverFileInfo.LastWriteTimeUtc.ToFileTimeStruct()
                    }
                },
                Flags = CldApi.CF_PLACEHOLDER_CREATE_FLAGS.CF_PLACEHOLDER_CREATE_FLAG_MARK_IN_SYNC,
            };
        }
        catch (Exception ex)
        {
            _relativePathPointer?.Dispose();
            throw new InvalidOperationException($"Failed to create SafeCoTaskMemString: {ex.Message}", ex);
        }
    }

    public SafeCreateInfo(RemoteDirectoryInfo serverDirectoryInfo, string relativePath, bool onDemand = true)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            throw new ArgumentException("Relative path cannot be null or empty", nameof(relativePath));
        }
        
        try
        {
            _relativePathPointer = new SafeCoTaskMemString(relativePath);
            CreateInfo = new CldApi.CF_PLACEHOLDER_CREATE_INFO
            {
                RelativeFileName = serverDirectoryInfo.Name,
                FileIdentity = _relativePathPointer,
                FileIdentityLength = _relativePathPointer.Size,
                FsMetadata = new CldApi.CF_FS_METADATA
                {
                    FileSize = 0,
                    BasicInfo = new Kernel32.FILE_BASIC_INFO
                    {
                        FileAttributes = (FileFlagsAndAttributes)serverDirectoryInfo.Attributes,
                        CreationTime = serverDirectoryInfo.CreationTimeUtc.ToFileTimeStruct(),
                        LastWriteTime = serverDirectoryInfo.LastWriteTimeUtc.ToFileTimeStruct(),
                        LastAccessTime = serverDirectoryInfo.LastAccessTimeUtc.ToFileTimeStruct(),
                        ChangeTime = serverDirectoryInfo.LastWriteTimeUtc.ToFileTimeStruct()
                    }
                },
                Flags = CldApi.CF_PLACEHOLDER_CREATE_FLAGS.CF_PLACEHOLDER_CREATE_FLAG_MARK_IN_SYNC
                    | (!onDemand ? CldApi.CF_PLACEHOLDER_CREATE_FLAGS.CF_PLACEHOLDER_CREATE_FLAG_DISABLE_ON_DEMAND_POPULATION : CldApi.CF_PLACEHOLDER_CREATE_FLAGS.CF_PLACEHOLDER_CREATE_FLAG_NONE),
            };
        }
        catch (Exception ex)
        {
            _relativePathPointer?.Dispose();
            throw new InvalidOperationException($"Failed to create SafeCoTaskMemString: {ex.Message}", ex);
        }
    }

    public static implicit operator CldApi.CF_PLACEHOLDER_CREATE_INFO(SafeCreateInfo c) => c.CreateInfo;

    public void Dispose()
    {
        _relativePathPointer.Dispose();
    }
}
