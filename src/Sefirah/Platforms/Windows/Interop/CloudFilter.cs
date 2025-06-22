using System.Runtime.InteropServices;
using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.Interop.Extensions;
using Sefirah.Platforms.Windows.Interop.SyncRoot;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;

namespace Sefirah.Platforms.Windows.Interop;
public static class CloudFilter
{
    public static CldApi.CF_CALLBACK_REGISTRATION[] ConnectSyncRoot(
        string syncRootPath,
        SyncRootEvents? events,
        out CldApi.CF_CONNECTION_KEY connectionKey
    )
    {
        var callbackRegistrations = events?.ToRegistrationArray() ?? [CldApi.CF_CALLBACK_REGISTRATION.CF_CALLBACK_REGISTRATION_END];
        CldApi.CfConnectSyncRoot(
            syncRootPath,
            callbackRegistrations,
            // optional callbackContext
            ConnectFlags: CldApi.CF_CONNECT_FLAGS.CF_CONNECT_FLAG_REQUIRE_PROCESS_INFO
                | CldApi.CF_CONNECT_FLAGS.CF_CONNECT_FLAG_REQUIRE_FULL_FILE_PATH
                | CldApi.CF_CONNECT_FLAGS.CF_CONNECT_FLAG_BLOCK_SELF_IMPLICIT_HYDRATION,
            ConnectionKey: out connectionKey
        ).ThrowIfFailed("Connect sync root failed");
        return callbackRegistrations;
    }

    public static void DisconnectSyncRoot(CldApi.CF_CONNECTION_KEY connectionKey) =>
        CldApi.CfDisconnectSyncRoot(connectionKey)
            .ThrowIfFailed("Disconnect sync root failed");

    public static bool IsPlaceholder(string path)
    {
        try
        {
            using var hfile = CreateHFile(path);
            return IsPlaceholder(hfile);
        }
        catch (HFileException)
        {
            // If we can't open the file handle, assume it's not a placeholder
            return false;
        }
    }

    public static CldApi.CF_PLACEHOLDER_STATE GetPlaceholderState(string path)
    {
        try
        {
            using var hfile = CreateHFile(path);
            return GetPlaceholderState(hfile);
        }
        catch (HFileException)
        {
            // return invalid
            return CldApi.CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_INVALID;
        }
    }

    public static CldApi.CF_PLACEHOLDER_STATE GetPlaceholderState(HFILE hfile)
    {
        Kernel32.FILE_ATTRIBUTE_TAG_INFO info = Kernel32.GetFileInformationByHandleEx<Kernel32.FILE_ATTRIBUTE_TAG_INFO>(
            hfile,
            Kernel32.FILE_INFO_BY_HANDLE_CLASS.FileAttributeTagInfo
        );
        var state = CldApi.CfGetPlaceholderStateFromAttributeTag(info.FileAttributes, info.ReparseTag);
        if (state == CldApi.CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_INVALID)
        {
            throw new Exception("Invalid placeholder state");
        }
        return state;
    }

    public static bool IsPlaceholder(HFILE hfile) =>
        GetPlaceholderState(hfile).HasFlag(CldApi.CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_PLACEHOLDER);

    public static void ConvertToPlaceholder(string path)
    {
        using var hfile = CreateHFile(path, FileAccess.Write);
        ConvertToPlaceholder(hfile);
    }

    /// <param name="hfile">Needs write</param>
    /// <remarks>Needs write</remarks>
    public static void ConvertToPlaceholder(HFILE hfile)
    {
        CldApi.CfConvertToPlaceholder(
            hfile,
            FileIdentity: default,
            FileIdentityLength: 0,
            CldApi.CF_CONVERT_FLAGS.CF_CONVERT_FLAG_NONE,
            out var _
        ).ThrowIfFailed("Convert to placeholder failed");
    }

    public static void HydratePlaceholder(string file)
    {
        using var hfile = CreateHFile(file);
        HydratePlaceholder(hfile);
    }

    /// <param name="hfile">Needs READ_DATA or WRITE_DAC</param>
    /// <remarks>Needs READ_DATA or WRITE_DAC</remarks>
    public static void HydratePlaceholder(HFILE hfile)
    {
        CldApi.CfHydratePlaceholder(hfile, HydrateFlags: CldApi.CF_HYDRATE_FLAGS.CF_HYDRATE_FLAG_NONE)
            .ThrowIfFailed("Hydrate placeholder failed");
    }

    public static long DehydratePlaceholder(string file, string relativePath, long fileSize)
    {
        using var hfile = CreateHFile(file, FileAccess.Write);
        return DehydratePlaceholder(hfile, relativePath, fileSize);
    }

    /// <param name="hfile">WRITE_DATA or WRITE_DAC, and oplock</param>
    /// <remarks>
    /// Requires WRITE_DATA or WRITE_DAC, and oplock.
    /// Use <see cref="CreateHFileWithOplock"/>
    /// </remarks>
    public static long DehydratePlaceholder(HFILE hfile, string relativePath, long fileSize) =>
        UpdateFilePlaceholder(hfile, relativePath, fileSize, fileInfo: null, dehydrate: true);

    /// <param name="hfile">WRITE_DATA or WRITE_DAC, and oplock</param>
    /// <remarks>
    /// Requires WRITE_DATA or WRITE_DAC, and oplock.
    /// Use <see cref="CreateHFileWithOplock"/>
    /// </remarks>
    public static long UpdateAndDehydratePlaceholder(HFILE hfile, string relativePath, RemoteFileInfo fileInfo) =>
        UpdateFilePlaceholder(hfile, relativePath, fileInfo.Length, fileInfo, dehydrate: true);

    /// <param name="hfile">WRITE_DATA or WRITE_DAC</param>
    /// <remarks>WRITE_DATA or WRITE_DAC; oplock for dehydrate</remarks>
    public static long UpdateFilePlaceholder(HFILE hfile, string relativePath, RemoteFileInfo fileInfo) =>
        UpdateFilePlaceholder(hfile, relativePath, fileInfo.Length, fileInfo, dehydrate: false);

    private static long UpdateFilePlaceholder(
        HFILE hfile,
        string relativePath,
        long fileSize,
        RemoteFileInfo? fileInfo,
        bool dehydrate
    )
    {
        using var relativePathPointer = new SafeCoTaskMemString(relativePath);
        var usn = 0L;
        CldApi.CfUpdatePlaceholder(
            hfile,
            new CldApi.CF_FS_METADATA
            {
                FileSize = fileSize,
                BasicInfo = new Kernel32.FILE_BASIC_INFO
                {
                    FileAttributes = (FileFlagsAndAttributes)(fileInfo?.Attributes ?? 0),
                    CreationTime = fileInfo?.LastWriteTimeUtc.ToFileTimeStruct() ?? FileTimeHelper.Zero,
                    LastWriteTime = fileInfo?.LastWriteTimeUtc.ToFileTimeStruct() ?? FileTimeHelper.Zero,
                    LastAccessTime = fileInfo?.LastAccessTimeUtc.ToFileTimeStruct() ?? FileTimeHelper.Zero,
                    ChangeTime = fileInfo?.LastWriteTimeUtc.ToFileTimeStruct() ?? FileTimeHelper.Zero
                },
            },
            relativePathPointer,
            relativePathPointer.Size,
            null,
            0,
            CldApi.CF_UPDATE_FLAGS.CF_UPDATE_FLAG_MARK_IN_SYNC
                | (dehydrate ? CldApi.CF_UPDATE_FLAGS.CF_UPDATE_FLAG_DEHYDRATE : CldApi.CF_UPDATE_FLAGS.CF_UPDATE_FLAG_NONE),
            ref usn
        ).ThrowIfFailed("Update placeholder failed");
        return usn;
    }

    /// <param name="hfile">Needs WRITE_DATA or WRITE_DAC</param>
    /// <remarks>Needs WRITE_DATA or WRITE_DAC</remarks>
    public static void UpdateDirectoryPlaceholder(HFILE hfile, RemoteDirectoryInfo directoryInfo, string relativePath)
    {
        using var relativeNamePointer = new SafeCoTaskMemString(relativePath);
        var usn = 0L;
        CldApi.CfUpdatePlaceholder(
            hfile,
            new CldApi.CF_FS_METADATA
            {
                FileSize = 0,
                BasicInfo = new Kernel32.FILE_BASIC_INFO
                {
                    FileAttributes = (FileFlagsAndAttributes)directoryInfo.Attributes,
                    CreationTime = directoryInfo.CreationTimeUtc.ToFileTimeStruct(),
                    LastWriteTime = directoryInfo.LastWriteTimeUtc.ToFileTimeStruct(),
                    LastAccessTime = directoryInfo.LastAccessTimeUtc.ToFileTimeStruct(),
                    ChangeTime = directoryInfo.LastWriteTimeUtc.ToFileTimeStruct()
                },
            },
            relativeNamePointer,
            relativeNamePointer.Size,
            null,
            0,
            CldApi.CF_UPDATE_FLAGS.CF_UPDATE_FLAG_MARK_IN_SYNC | CldApi.CF_UPDATE_FLAGS.CF_UPDATE_FLAG_DISABLE_ON_DEMAND_POPULATION,
            ref usn
        ).ThrowIfFailed("Update placeholder failed");
    }

    public static SafeMetaHFILE CreateHFile(string path, FileAccess access = 0, FileShare fileShare = FileShare.Read)
    {
        var handle = Kernel32.CreateFile(
            path,
            (Kernel32.FileAccess)access | Kernel32.FileAccess.FILE_READ_ATTRIBUTES,
            fileShare,
            dwCreationDisposition: FileMode.Open,
            dwFlagsAndAttributes: FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS
        ).ToMeta();
        
        return handle.ThrowIfInvalid(path);
    }

    public static SafeMetaHFILE CreateHFileWithOplock(string clientPath, FileAccess access = FileAccess.Read)
    {
        var hr = CldApi.CfOpenFileWithOplock(clientPath, (CldApi.CF_OPEN_FILE_FLAGS)access | CldApi.CF_OPEN_FILE_FLAGS.CF_OPEN_FILE_FLAG_EXCLUSIVE, out var hcffile);
        hr.ThrowIfFailed("Failed to open hfile");
        return hcffile.ToMeta().ThrowIfInvalid(clientPath);
    }

    public readonly static NTStatus STATUS_CLOUD_FILE_UNSUCCESSFUL = new(0xc000cf12);
    public static void AckRename(CldApi.CF_CALLBACK_INFO callbackInfo, bool success = true)
    {
        var opInfo = new CldApi.CF_OPERATION_INFO
        {
            StructSize = (uint)Marshal.SizeOf<CldApi.CF_OPERATION_INFO>(),
            Type = CldApi.CF_OPERATION_TYPE.CF_OPERATION_TYPE_ACK_RENAME,
            ConnectionKey = callbackInfo.ConnectionKey,
            TransferKey = callbackInfo.TransferKey,
            RequestKey = callbackInfo.RequestKey,
        };
        var opParameters = CldApi.CF_OPERATION_PARAMETERS.Create(
            new CldApi.CF_OPERATION_PARAMETERS.ACKRENAME
            {
                CompletionStatus = success
                    ? NTStatus.STATUS_SUCCESS
                    : STATUS_CLOUD_FILE_UNSUCCESSFUL
            }
        );
        CldApi.CfExecute(opInfo, ref opParameters).ThrowIfFailed("Ack Rename failed");
    }

    public static void AckDelete(CldApi.CF_CALLBACK_INFO callbackInfo, bool success = true)
    {
        var opInfo = callbackInfo.ToOperationInfo(CldApi.CF_OPERATION_TYPE.CF_OPERATION_TYPE_ACK_DELETE);
        var opParameters = CldApi.CF_OPERATION_PARAMETERS.Create(
            new CldApi.CF_OPERATION_PARAMETERS.ACKDELETE
            {
                CompletionStatus = success
                    ? NTStatus.STATUS_SUCCESS
                    : STATUS_CLOUD_FILE_UNSUCCESSFUL
            }
        );
        CldApi.CfExecute(opInfo, ref opParameters).ThrowIfFailed("Ack Delete failed");
    }

    public static void TransferPlaceholders(CldApi.CF_CALLBACK_INFO callbackInfo, RemoteFileSystemInfo[] remoteInfos)
    {
        if (remoteInfos.Length == 0)
        {
            var opInfo = callbackInfo.ToOperationInfo(CldApi.CF_OPERATION_TYPE.CF_OPERATION_TYPE_TRANSFER_PLACEHOLDERS);
            var opParameters = CldApi.CF_OPERATION_PARAMETERS.Create(
                new CldApi.CF_OPERATION_PARAMETERS.TRANSFERPLACEHOLDERS
                {
                    CompletionStatus = NTStatus.STATUS_SUCCESS,
                    EntriesProcessed = 0,
                    PlaceholderArray = 0,
                    PlaceholderCount = 0,
                    PlaceholderTotalCount = 0,
                    Flags = CldApi.CF_OPERATION_TRANSFER_PLACEHOLDERS_FLAGS.CF_OPERATION_TRANSFER_PLACEHOLDERS_FLAG_STOP_ON_ERROR
                        | CldApi.CF_OPERATION_TRANSFER_PLACEHOLDERS_FLAGS.CF_OPERATION_TRANSFER_PLACEHOLDERS_FLAG_DISABLE_ON_DEMAND_POPULATION,
                }
            );
            CldApi.CfExecute(opInfo, ref opParameters).ThrowIfFailed("Transfer Placeholders failed");
            return;
        }

        // Can cause corrupt placeholders when done all at once for some reason
        // Seems to work one at a time
        var entriesProcessed = 0u;
        foreach (var remoteInfo in remoteInfos)
        {
            var opInfo = callbackInfo.ToOperationInfo(CldApi.CF_OPERATION_TYPE.CF_OPERATION_TYPE_TRANSFER_PLACEHOLDERS);
            using SafeCreateInfo createInfo = remoteInfo switch
            {
                RemoteFileInfo fileInfo => new SafeCreateInfo(fileInfo, remoteInfo.RelativePath),
                RemoteDirectoryInfo directoryInfo => new SafeCreateInfo(directoryInfo, remoteInfo.RelativePath),
                _ => throw new ArgumentException("Unexpected server info", nameof(remoteInfos)),
            };
            CldApi.CF_PLACEHOLDER_CREATE_INFO[] raw = [createInfo];
            using SafeHGlobalHandle createInfoArrayPointer = SafeHGlobalHandle.CreateFromList(raw);
            var opParameters = CldApi.CF_OPERATION_PARAMETERS.Create(
                new CldApi.CF_OPERATION_PARAMETERS.TRANSFERPLACEHOLDERS
                {
                    CompletionStatus = NTStatus.STATUS_SUCCESS,
                    EntriesProcessed = entriesProcessed,
                    PlaceholderArray = createInfoArrayPointer,
                    PlaceholderCount = 1,
                    PlaceholderTotalCount = Convert.ToUInt32(remoteInfos.Length),
                    Flags = CldApi.CF_OPERATION_TRANSFER_PLACEHOLDERS_FLAGS.CF_OPERATION_TRANSFER_PLACEHOLDERS_FLAG_STOP_ON_ERROR
                        | CldApi.CF_OPERATION_TRANSFER_PLACEHOLDERS_FLAGS.CF_OPERATION_TRANSFER_PLACEHOLDERS_FLAG_DISABLE_ON_DEMAND_POPULATION,
                }
            );
            CldApi.CfExecute(opInfo, ref opParameters).ThrowIfFailed("Transfer Placeholders failed");
            entriesProcessed++;
        }
    }

    public static void FailTransferPlaceholders(CldApi.CF_CALLBACK_INFO callbackInfo)
    {
        var opInfo = callbackInfo.ToOperationInfo(CldApi.CF_OPERATION_TYPE.CF_OPERATION_TYPE_TRANSFER_PLACEHOLDERS);
        var opParameters = CldApi.CF_OPERATION_PARAMETERS.Create(
            new CldApi.CF_OPERATION_PARAMETERS.TRANSFERPLACEHOLDERS
            {
                CompletionStatus = STATUS_CLOUD_FILE_UNSUCCESSFUL,
            }
        );
        CldApi.CfExecute(opInfo, ref opParameters).ThrowIfFailed("Fail Transfer Placeholders failed");
    }

    public static void TransferData(CldApi.CF_CALLBACK_INFO callbackInfo, byte[]? data, long offset, long length, bool success = true)
    {
        var opInfo = callbackInfo.ToOperationInfo(CldApi.CF_OPERATION_TYPE.CF_OPERATION_TYPE_TRANSFER_DATA);
        using var buf = new PinnedObject(data);
        var opParameters = CldApi.CF_OPERATION_PARAMETERS.Create(
            new CldApi.CF_OPERATION_PARAMETERS.TRANSFERDATA
            {
                CompletionStatus = success
                    ? 0x0 // STATUS_SUCCESS
                    : 0xC0000001, // STATUS_UNSUCCESSFUL,
                Buffer = buf,
                Offset = offset,
                Length = length,
            }
        );
        var hr = CldApi.CfExecute(opInfo, ref opParameters);
        // expected ERROR_CLOUD_REQUEST_CANCELED
        if (hr.Code != 398)
        {
            hr.ThrowIfFailed("Update transfer data failed");
        };
    }

    public static void ReportProgress(CldApi.CF_CALLBACK_INFO callbackInfo, long total, long completed) =>
        CldApi.CfReportProviderProgress(
            callbackInfo.ConnectionKey,
            callbackInfo.TransferKey,
            total,
            completed
        ).ThrowIfFailed("Report progress failed");

    public static void SetInSyncState(string clientPath)
    {
        using var hfile = CreateHFile(clientPath, FileAccess.Write);
        SetInSyncState(hfile);
    }

    /// <param name="hfile">Needs WRITE_DATA or WRITE_DAC</param>
    /// <remarks>Needs WRITE_DATA or WRITE_DAC</remarks>
    public static void SetInSyncState(HFILE hfile, long usn = 0L)
    {
        CldApi.CfSetInSyncState(
            hfile,
            CldApi.CF_IN_SYNC_STATE.CF_IN_SYNC_STATE_IN_SYNC,
            CldApi.CF_SET_IN_SYNC_FLAGS.CF_SET_IN_SYNC_FLAG_NONE,
            ref usn
        ).ThrowIfFailed("Set in-sync state failed");
    }

    public static void ClearInSyncState(string clientPath)
    {
        using var hfile = CreateHFile(clientPath, FileAccess.Write);
        ClearInSyncState(hfile);
    }

    /// <param name="hfile">Needs WRITE_DATA or WRITE_DAC</param>
    /// <remarks>Needs WRITE_DATA or WRITE_DAC</remarks>
    public static void ClearInSyncState(HFILE hfile, long usn = 0L)
    {
        CldApi.CfSetInSyncState(
            hfile,
            CldApi.CF_IN_SYNC_STATE.CF_IN_SYNC_STATE_NOT_IN_SYNC,
            CldApi.CF_SET_IN_SYNC_FLAGS.CF_SET_IN_SYNC_FLAG_NONE,
            ref usn
        ).ThrowIfFailed("Clear in-sync state failed");
    }


    public static void SetPinnedState(string clientPath, int pinned)
    {
        using var hfile = CreateHFile(clientPath);
        SetPinnedState(hfile, pinned);
    }

    public static void SetPinnedState(HFILE hfile, int pinned)
    {
        var pinState = SyncAttributes.HasAnyFlag(pinned, SyncAttributes.PINNED)
            ? CldApi.CF_PIN_STATE.CF_PIN_STATE_PINNED
            : SyncAttributes.HasAnyFlag(pinned, SyncAttributes.UNPINNED)
            ? CldApi.CF_PIN_STATE.CF_PIN_STATE_UNPINNED
            : CldApi.CF_PIN_STATE.CF_PIN_STATE_UNSPECIFIED;
        CldApi.CfSetPinState(hfile, pinState, CldApi.CF_SET_PIN_FLAGS.CF_SET_PIN_FLAG_NONE);
    }

    public static void UpdateSyncRoot(string syncRootPath, byte[] context)
    {
        using var contextPtr = new SafeHGlobalHandle(context);
        var registration = new CldApi.CF_SYNC_REGISTRATION
        {
            StructSize = (uint)Marshal.SizeOf<CldApi.CF_SYNC_REGISTRATION>(),
            ProviderName = "Sefirah",
            ProviderVersion = "1.0",
            SyncRootIdentity = contextPtr,
            SyncRootIdentityLength = (uint)context.Length,
            FileIdentity = nint.Zero,
            FileIdentityLength = 0,
        };

        var policies = new CldApi.CF_SYNC_POLICIES
        {
            StructSize = (uint)Marshal.SizeOf<CldApi.CF_SYNC_POLICIES>(),
            Hydration = new CldApi.CF_HYDRATION_POLICY 
            { 
                Primary = CldApi.CF_HYDRATION_POLICY_PRIMARY.CF_HYDRATION_POLICY_FULL,
                Modifier = CldApi.CF_HYDRATION_POLICY_MODIFIER.CF_HYDRATION_POLICY_MODIFIER_AUTO_DEHYDRATION_ALLOWED
            },
            Population = new CldApi.CF_POPULATION_POLICY
            {
                Primary = CldApi.CF_POPULATION_POLICY_PRIMARY.CF_POPULATION_POLICY_FULL
            },
            InSync = CldApi.CF_INSYNC_POLICY.CF_INSYNC_POLICY_TRACK_FILE_CREATION_TIME | 
                     CldApi.CF_INSYNC_POLICY.CF_INSYNC_POLICY_TRACK_DIRECTORY_CREATION_TIME |
                     CldApi.CF_INSYNC_POLICY.CF_INSYNC_POLICY_TRACK_FILE_LAST_WRITE_TIME |
                     CldApi.CF_INSYNC_POLICY.CF_INSYNC_POLICY_TRACK_DIRECTORY_LAST_WRITE_TIME,
            HardLink = CldApi.CF_HARDLINK_POLICY.CF_HARDLINK_POLICY_NONE,
        };

        CldApi.CfRegisterSyncRoot(
            syncRootPath,
            in registration,
            in policies,
            CldApi.CF_REGISTER_FLAGS.CF_REGISTER_FLAG_UPDATE
        ).ThrowIfFailed("Update sync root failed");
    }

    internal static class FileTimeHelper
    {
        public static System.Runtime.InteropServices.ComTypes.FILETIME Zero = new System.Runtime.InteropServices.ComTypes.FILETIME
        {
            dwHighDateTime = 0,
            dwLowDateTime = 0,
        };
    }
}
