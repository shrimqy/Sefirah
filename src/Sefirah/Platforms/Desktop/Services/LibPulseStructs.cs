using System.Runtime.InteropServices;

namespace Sefirah.Platforms.Desktop.Services;

/// <summary>
/// Managed layouts for libpulse introspection structs (linux-x64, libpulse 17.x ABI).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PaSampleSpec
{
    public int Format;
    public uint Rate;
    public byte Channels;
    private byte _padding0;
    private byte _padding1;
    private byte _padding2;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PaChannelMap
{
    public byte Channels;
    private fixed byte _padding[3];
    private fixed int _map[32];
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PaCVolume
{
    public byte Channels;
    private fixed byte _padding[3];
    public fixed uint Values[32];
}

[StructLayout(LayoutKind.Sequential)]
internal struct PaSinkInfo
{
    public IntPtr Name;
    public uint Index;
    private uint _paddingAfterIndex;
    public IntPtr Description;
    public PaSampleSpec SampleSpec;
    public PaChannelMap ChannelMap;
    public uint OwnerModule;
    public PaCVolume Volume;
    public int Mute;
    public uint MonitorSource;
    public IntPtr MonitorSourceName;
    public ulong Latency;
    public IntPtr Driver;
    public uint Flags;
    public IntPtr Proplist;
    public ulong ConfiguredLatency;
    public uint BaseVolume;
    public uint State;
    public uint NVolumeSteps;
    public uint Card;
    public uint NPorts;
    public IntPtr Ports;
    public IntPtr ActivePort;
    public byte NFormats;
    private byte _padding0;
    private byte _padding1;
    private byte _padding2;
    public IntPtr Formats;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PaServerInfo
{
    public IntPtr UserName;
    public IntPtr HostName;
    public IntPtr ServerVersion;
    public IntPtr ServerName;
    public PaSampleSpec SampleSpec;
    private uint _paddingAfterSampleSpec;
    public IntPtr DefaultSinkName;
    public IntPtr DefaultSourceName;
    public uint Cookie;
    public PaChannelMap ChannelMap;
}

internal static class LibPulseStructs
{
    internal static void ValidateLayouts()
    {
        if (!OperatingSystem.IsLinux())
            return;

        Debug.Assert(Marshal.SizeOf<PaCVolume>() == LibPulseInterop.CVolumeSize);
        Debug.Assert(Marshal.OffsetOf<PaSinkInfo>(nameof(PaSinkInfo.Name)) == 0);
        Debug.Assert(Marshal.OffsetOf<PaSinkInfo>(nameof(PaSinkInfo.Index)) == 8);
        Debug.Assert(Marshal.OffsetOf<PaSinkInfo>(nameof(PaSinkInfo.Description)) == 16);
        Debug.Assert(Marshal.OffsetOf<PaSinkInfo>(nameof(PaSinkInfo.Volume)) == 172);
        Debug.Assert(Marshal.OffsetOf<PaSinkInfo>(nameof(PaSinkInfo.Mute)) == 304);
        Debug.Assert(Marshal.OffsetOf<PaServerInfo>(nameof(PaServerInfo.DefaultSinkName)) == 48);
    }

    internal static bool TryReadSinkInfo(IntPtr pointer, out PaSinkInfo sinkInfo)
    {
        sinkInfo = default;
        if (pointer == IntPtr.Zero)
            return false;

        sinkInfo = Marshal.PtrToStructure<PaSinkInfo>(pointer);
        return true;
    }

    internal static bool TryReadServerInfo(IntPtr pointer, out PaServerInfo serverInfo)
    {
        serverInfo = default;
        if (pointer == IntPtr.Zero)
            return false;

        serverInfo = Marshal.PtrToStructure<PaServerInfo>(pointer);
        return true;
    }
}
