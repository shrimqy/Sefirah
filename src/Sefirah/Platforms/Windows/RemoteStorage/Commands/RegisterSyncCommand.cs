using System.Runtime.InteropServices;

namespace Sefirah.Platforms.Windows.RemoteStorage.Commands;
[StructLayout(LayoutKind.Sequential)]
public struct RegisterSyncRootCommand
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string Name;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string AccountId;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 500)]
    public string Directory;
    public PopulationPolicy PopulationPolicy;
}
