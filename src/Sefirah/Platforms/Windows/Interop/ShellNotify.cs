using static Vanara.PInvoke.Shell32;

namespace Sefirah.Platforms.Windows.Interop;

public static class ShellNotify
{
    public static void NotifyDelete(string clientPath, bool isDirectory)
    {
        SHChangeNotify(isDirectory ? SHCNE.SHCNE_RMDIR : SHCNE.SHCNE_DELETE, SHCNF.SHCNF_PATHW, clientPath, null);

        // Parent refresh - delete event alone often doesn't update the open folder view.
        var parentPath = Path.GetDirectoryName(clientPath);
        if (parentPath is not null)
        {
            SHChangeNotify(SHCNE.SHCNE_UPDATEDIR, SHCNF.SHCNF_PATHW, parentPath, null);
        }
    }

    public static void NotifyUpdate(string clientPath)
    {
        SHChangeNotify(SHCNE.SHCNE_UPDATEITEM, SHCNF.SHCNF_PATHW, clientPath, null);
    }
}
