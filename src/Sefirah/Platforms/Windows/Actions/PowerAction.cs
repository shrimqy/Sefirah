using System.Runtime.InteropServices;
using Sefirah.Actions.Power;
using Sefirah.Utils;

namespace Sefirah.Actions.Power;

public sealed partial class PowerAction
{
    private const uint EWX_LOGOFF = 0x00000000;
    private const uint EWX_SHUTDOWN = 0x00000001;
    private const uint EWX_REBOOT = 0x00000002;
    private const uint EWX_FORCE = 0x00000004;
    private const uint EWX_POWEROFF = 0x00000008;
    private const uint SHTDN_REASON_MAJOR_OTHER = 0x00000000;
    private const uint SHTDN_REASON_FLAG_PLANNED = 0x80000000;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    public Task ExecuteAsync()
    {
        var kind = item.Get<PowerSettings>().Kind;
        var logger = Ioc.Default.GetRequiredService<ILogger>();

        return Task.Run(() =>
        {
            try
            {
                switch (kind)
                {
                    case PowerKind.Lock:
                        if (!LockWorkStation())
                        {
                            logger.Warn("LockWorkStation failed");
                        }
                        break;

                    case PowerKind.LogOff:
                        ExitWindowsEx(EWX_LOGOFF | EWX_FORCE, SHTDN_REASON_MAJOR_OTHER | SHTDN_REASON_FLAG_PLANNED);
                        break;

                    case PowerKind.Sleep:
                        if (!SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false))
                        {
                            logger.Warn("SetSuspendState(sleep) failed");
                        }
                        break;

                    case PowerKind.Hibernate:
                        if (!SetSuspendState(hibernate: true, forceCritical: false, disableWakeEvent: false))
                        {
                            logger.Warn("SetSuspendState(hibernate) failed");
                        }
                        break;

                    case PowerKind.Restart:
                        if (!TryExitWindows(EWX_REBOOT | EWX_FORCE))
                        {
                            StartShutdownCommand("/r /t 0");
                        }
                        break;

                    case PowerKind.Shutdown:
                        if (!TryExitWindows(EWX_SHUTDOWN | EWX_POWEROFF | EWX_FORCE))
                        {
                            StartShutdownCommand("/s /t 0");
                        }
                        break;

                    default:
                        logger.Warn($"Unhandled power kind: {kind}");
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error executing power kind {kind}", ex);
            }
        });
    }

    private static bool TryExitWindows(uint flags)
    {
        if (!EnableShutdownPrivilege())
        {
            return false;
        }

        return ExitWindowsEx(flags, SHTDN_REASON_MAJOR_OTHER | SHTDN_REASON_FLAG_PLANNED);
    }

    private static void StartShutdownCommand(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }

    private static bool EnableShutdownPrivilege()
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var tokenHandle))
        {
            return false;
        }

        try
        {
            if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out var luid))
            {
                return false;
            }

            var privileges = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                }
            };

            if (!AdjustTokenPrivileges(tokenHandle, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
            {
                return false;
            }

            return Marshal.GetLastWin32Error() == 0;
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    [DllImport("PowrProf.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privileges;
    }
}
