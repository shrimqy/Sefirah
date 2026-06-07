using Vanara.PInvoke;

namespace Sefirah.Platforms.Windows.Interop;

public abstract partial class SafeMetaHFILE : IDisposable
{
    public abstract HFILE FileHandle { get; }
    public static implicit operator HFILE(SafeMetaHFILE h) => h.FileHandle;
    public abstract void Dispose();

    public sealed partial class Kernel32HFILE(Kernel32.SafeHFILE fileHandle) : SafeMetaHFILE
    {
        public override HFILE FileHandle => fileHandle;
        public override void Dispose()
        {
            fileHandle.Dispose();
        }
    }

    public sealed partial class OplockHFILE(SafeOplockHFILE fileHandle) : SafeMetaHFILE
    {
        public override HFILE FileHandle => fileHandle;
        public override void Dispose()
        {
            fileHandle.Dispose();
        }
    }
}
