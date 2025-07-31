using System.Runtime.InteropServices;

namespace Sefirah.Platforms.Windows.Interop;
public static class StructBytes
{
    public static byte[] ToBytes<T>(T source) where T : struct
    {
        int size = Marshal.SizeOf(source);
        byte[] bytes = new byte[size];

        nint ptr = nint.Zero;
        try
        {
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(source, ptr, true);
            Marshal.Copy(ptr, bytes, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
        return bytes;
    }

    public static T FromBytes<T>(byte[] bytes) where T : struct
    {
        T str = new();

        int size = Marshal.SizeOf(str);
        nint ptr = nint.Zero;
        try
        {
            ptr = Marshal.AllocHGlobal(size);

            Marshal.Copy(bytes, 0, ptr, size);

            str = (T)Marshal.PtrToStructure(ptr, typeof(T))!;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
        return str;
    }
}
