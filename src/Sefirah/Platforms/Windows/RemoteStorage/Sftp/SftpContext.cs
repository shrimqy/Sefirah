using System.Runtime.InteropServices;

namespace Sefirah.Platforms.Windows.RemoteStorage.Sftp;
[StructLayout(LayoutKind.Sequential)]
public struct SftpContext
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string Directory;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string Host;
    public int Port;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string Username;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string Password;
    public int WatchPeriodSeconds;

    public SftpContext()
    {
        Directory = string.Empty;
        Host = string.Empty;
        Port = 22;
        Username = string.Empty;
        Password = string.Empty;
        WatchPeriodSeconds = 2;
    }
}
