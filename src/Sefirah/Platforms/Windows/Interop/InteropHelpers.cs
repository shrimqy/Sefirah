using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;

namespace Sefirah.Platforms.Windows.Interop;

[ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPolicyConfig
{
    void NotImplemented1();
    void NotImplemented2();
    void NotImplemented3();
    void NotImplemented4();
    void NotImplemented5();
    void NotImplemented6();
    void NotImplemented7();
    void NotImplemented8();
    void NotImplemented9();
    void NotImplemented10();

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, [MarshalAs(UnmanagedType.I4)] ERole role);
}

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2
}


public static class InteropHelpers
{
    public static readonly Guid DataTransferManagerInteropIID = new(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern nint CreateEvent(nint lpEventAttributes, bool bManualReset,
            bool bInitialState, string lpName);

    [DllImport("kernel32.dll")]
    public static extern bool SetEvent(nint hEvent);

    [DllImport("ole32.dll")]
    public static extern uint CoWaitForMultipleObjects(uint dwFlags, uint dwMilliseconds, ulong nHandles, nint[] pHandles, out uint dwIndex);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
            => (X, Y) = (x, y);
    }

    public static void ChangeCursor(this UIElement uiElement, InputCursor cursor)
    {
        Type type = typeof(UIElement);
        type.InvokeMember("ProtectedCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, uiElement, new object[] { cursor });
    }
}
