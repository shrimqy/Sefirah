using Sefirah.App.RemoteStorage.Interop.Extensions;
using Vanara.PInvoke;
using System;
using System.Runtime.InteropServices;

namespace Sefirah.App.RemoteStorage.Interop;
public static class HFileExtensions
{
    public static SafeMetaHFILE ToMeta(this Kernel32.SafeHFILE fileHandle) => new SafeMetaHFILE.Kernel32HFILE(fileHandle);
    public static SafeMetaHFILE ToMeta(this SafeOplockHFILE fileHandle) => new SafeMetaHFILE.OplockHFILE(fileHandle);
    public static SafeMetaHFILE ToMeta(this CldApi.SafeHCFFILE fileHandle) => new SafeOplockHFILE(fileHandle).ToMeta();
    public static SafeMetaHFILE ThrowIfInvalid(this SafeMetaHFILE fileHandle, string path)
    {
        if (!((HFILE)fileHandle).IsInvalid)
        {
            return fileHandle;
        }
        
        var lastError = Marshal.GetLastWin32Error();
        fileHandle.Dispose();
        
        throw new HFileException($"Failed to create valid file handle for: {path}", lastError, path);
    }
}
