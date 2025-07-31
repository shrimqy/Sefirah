using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.Kernel32;

namespace Sefirah.Platforms.Windows.Helpers;


/// <summary>
/// Provides static helper for Win32.
/// </summary>
public static partial class Win32Helper
{
    /// <summary>
    /// Brings the app window to foreground.
    /// </summary>
    /// <remarks>
    /// For more information, visit
    /// <br/>
    /// - <a href="https://stackoverflow.com/questions/1544179/what-are-the-differences-between-bringwindowtotop-setforegroundwindow-setwindo" />
    /// <br/>
    /// - <a href="https://stackoverflow.com/questions/916259/win32-bring-a-window-to-top" />
    /// </remarks>
    /// <param name="hWnd">The window handle to bring.</param>
    public static void BringToForegroundEx(HWND hWnd)
    {
        var hCurWnd = GetForegroundWindow();
        var dwMyID = GetCurrentThreadId();
        var dwCurID = GetWindowThreadProcessId(hCurWnd, out _);

        AttachThreadInput(dwCurID, dwMyID, true);

        SetWindowPos(hWnd, (HWND)(-1), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOMOVE);
        SetWindowPos(hWnd, (HWND)(-2), 0, 0, 0, 0, SetWindowPosFlags.SWP_SHOWWINDOW | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOMOVE);
        SetForegroundWindow(hWnd);
        SetFocus(hWnd);
        SetActiveWindow(hWnd);
        AttachThreadInput(dwCurID, dwMyID, false);
    }

    /// <summary>
    /// Force window to stay at bottom of other upper windows.
    /// </summary>
    /// <param name="lParam">The lParam of the message.</param>
    public static void ForceWindowPosition(nint lParam)
    {
        var windowPos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
        windowPos.flags |= SetWindowPosFlags.SWP_NOZORDER;
        Marshal.StructureToPtr(windowPos, lParam, false);
    }
}
