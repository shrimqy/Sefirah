using Vanara.PInvoke;

namespace Sefirah.Platforms.Windows.Interop;
public class SafeOplockHFILE : IDisposable
{
    private bool _isDisposed = false;
    private readonly CldApi.SafeHCFFILE _hcffile;
    public HFILE FileHandle { get; init; }

    public SafeOplockHFILE(CldApi.SafeHCFFILE hcffile)
    {
        _hcffile = hcffile;
        if (!CldApi.CfReferenceProtectedHandle(_hcffile))
        {
            var win32Error = Kernel32.GetLastError();
            throw new IOException("CfReferenceProtectedHandle failed", win32Error.ToHRESULT().Code);
        }
        FileHandle = CldApi.CfGetWin32HandleFromProtectedHandle(_hcffile);
    }

    public static implicit operator HFILE(SafeOplockHFILE h) => h.FileHandle;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        CldApi.CfReleaseProtectedHandle(_hcffile);
        _hcffile.Dispose();
        _isDisposed = true;
    }
}
