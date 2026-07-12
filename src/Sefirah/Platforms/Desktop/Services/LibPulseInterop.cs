using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sefirah.Platforms.Desktop.Services;

internal static class LibPulseInterop
{
    internal const uint VolumeNorm = 0x10000;
    internal const int CVolumeSize = 132;
    internal const int ContextReady = 4;
    internal const int ContextFailed = 5;
    internal const int ContextTerminated = 6;
    internal const int OperationRunning = 0;
    internal const uint SubscriptionEventFacilityMask = 0x0F;
    internal const uint SubscriptionEventTypeMask = 0x30;
    internal const uint SubscriptionEventChange = 0x10;
    internal const uint SubscriptionEventSink = 0x00;
    internal const uint SubscriptionEventServer = 0x07;
    internal const uint SubscriptionMaskSink = 1u;
    internal const uint SubscriptionMaskServer = 1u << 7;

    private const string LibraryName = "pulse";

    private static readonly string[] LibraryCandidates =
    [
        "libpulse.so.0",
        "libpulse.so",
        LibraryName
    ];

    private static int resolverInstalled;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void PaContextStateCallback(IntPtr context, IntPtr userdata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void PaServerInfoCallback(IntPtr context, IntPtr info, IntPtr userdata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void PaSinkInfoCallback(IntPtr context, IntPtr info, int endOfList, IntPtr userdata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void PaSubscribeCallback(IntPtr context, uint eventType, uint index, IntPtr userdata);

    internal static void EnsureResolver()
    {
        if (Interlocked.CompareExchange(ref resolverInstalled, 1, 0) != 0)
            return;

        LibPulseStructs.ValidateLayouts();
        NativeLibrary.SetDllImportResolver(typeof(LibPulseInterop).Assembly, ResolveLibrary);
    }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals(LibraryName, StringComparison.Ordinal))
            return IntPtr.Zero;

        foreach (var candidate in LibraryCandidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_threaded_mainloop_new();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int pa_threaded_mainloop_start(IntPtr mainloop);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_threaded_mainloop_stop(IntPtr mainloop);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_threaded_mainloop_free(IntPtr mainloop);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_threaded_mainloop_lock(IntPtr mainloop);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_threaded_mainloop_unlock(IntPtr mainloop);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_threaded_mainloop_wait(IntPtr mainloop);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_threaded_mainloop_signal(IntPtr mainloop, int waitForAccept);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_threaded_mainloop_get_api(IntPtr mainloop);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_new(IntPtr api, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_context_unref(IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int pa_context_connect(IntPtr context, IntPtr server, uint flags, IntPtr spawnApi);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_context_disconnect(IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int pa_context_get_state(IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_context_set_state_callback(IntPtr context, PaContextStateCallback? callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_get_server_info(IntPtr context, PaServerInfoCallback callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_get_sink_info_list(IntPtr context, PaSinkInfoCallback callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_get_sink_info_by_name(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        PaSinkInfoCallback callback,
        IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_get_sink_info_by_index(IntPtr context, uint index, PaSinkInfoCallback callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_set_sink_volume_by_name(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr volume,
        IntPtr callback,
        IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_set_sink_volume_by_index(IntPtr context, uint index, IntPtr volume, IntPtr callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_set_sink_mute_by_index(IntPtr context, uint index, int mute, IntPtr callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_set_default_sink(IntPtr context, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_context_set_subscribe_callback(IntPtr context, PaSubscribeCallback? callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_context_subscribe(IntPtr context, uint mask, IntPtr callback, IntPtr userdata);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int pa_operation_get_state(IntPtr operation);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_operation_unref(IntPtr operation);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint pa_cvolume_avg(IntPtr volume);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_cvolume_set(IntPtr volume, uint channels, uint volumeValue);

    internal static string? ReadUtf8String(IntPtr pointer) =>
        pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);

    internal static unsafe float ReadVolumeScalar(PaCVolume volume)
    {
        var average = pa_cvolume_avg((IntPtr)Unsafe.AsPointer(ref volume));
        return average / (float)VolumeNorm;
    }
}
