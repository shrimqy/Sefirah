using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Shell32;

namespace Sefirah.Platforms.Windows.HostedApp;

/// <summary>
/// Sets the hosted app AUMID on the scrcpy window so the taskbar groups it with the pinned tile.
/// </summary>
internal static class WindowShellBinder
{
    private const int WindowWaitTimeoutMs = 5000;
    private const int WindowPollIntervalMs = 50;

    private static readonly Guid PropertyStoreGuid = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    private static readonly PropertyKey AppUserModelIdKey = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    public static async Task TryBindAsync(
        Process process,
        string appUserModelId,
        CancellationToken cancellationToken = default)
    {
        if (process.HasExited || string.IsNullOrWhiteSpace(appUserModelId))
            return;

        var deadline = Environment.TickCount64 + WindowWaitTimeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
                return;

            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                TrySetAppUserModelId((HWND)process.MainWindowHandle, appUserModelId);
                return;
            }

            await Task.Delay(WindowPollIntervalMs, cancellationToken);
        }
    }

    private static void TrySetAppUserModelId(HWND hwnd, string appUserModelId)
    {
        IPropertyStore? propertyStore = null;
        try
        {
            var hr = SHGetPropertyStoreForWindow(hwnd, PropertyStoreGuid, out object? propertyStoreObj);
            if (hr.Failed || propertyStoreObj is null)
                return;

            propertyStore = (IPropertyStore)propertyStoreObj;
            using var value = new PropVariant(appUserModelId);
            propertyStore.SetValue(in AppUserModelIdKey, value);
            propertyStore.Commit();
        }
        catch
        {
            // Best-effort; mirroring still works without taskbar grouping.
        }
        finally
        {
            if (propertyStore is not null)
                Marshal.ReleaseComObject(propertyStore);
        }
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(in PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(in PropertyKey key, PropVariant pv);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PropVariant : IDisposable
    {
        private const ushort VT_LPWSTR = 31;

        [FieldOffset(0)] private ushort vt;
        [FieldOffset(8)] private nint pointer;

        public PropVariant(string value)
        {
            vt = VT_LPWSTR;
            pointer = Marshal.StringToCoTaskMemUni(value);
        }

        public readonly void Dispose()
        {
            if (vt == VT_LPWSTR && pointer != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pointer);
        }
    }
}
