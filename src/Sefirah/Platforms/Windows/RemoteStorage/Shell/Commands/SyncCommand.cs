using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Channels;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.ShlwApi;

namespace Sefirah.Platforms.Windows.RemoteStorage.Shell.Commands;
[ComVisible(true), Guid("942952b6-3bdc-4e50-8fe2-8d2d869ca70f")]
public class SyncCommand(
    ChannelWriter<ShellCommand> commandWriter,
    ILogger logger) : IExplorerCommand, IExplorerCommandState, IObjectWithSite
{
    public HRESULT GetTitle(IShellItemArray psiItemArray, out string? ppszName)
    {
        ppszName = "Sync From Cloud";
        return HRESULT.S_OK;
    }

    public HRESULT GetIcon(IShellItemArray psiItemArray, out string? ppszIcon)
    {
        ppszIcon = null;
        return HRESULT.E_NOTIMPL;
    }

    public HRESULT GetToolTip(IShellItemArray psiItemArray, out string? ppszInfotip)
    {
        ppszInfotip = null;
        return HRESULT.E_NOTIMPL;
    }

    public HRESULT GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = Guid.Empty;
        return HRESULT.E_NOTIMPL;
    }

    public HRESULT GetState(IShellItemArray psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        pCmdState = EXPCMDSTATE.ECS_ENABLED;
        return HRESULT.S_OK;
    }

    public HRESULT Invoke(IShellItemArray psiItemArray, IBindCtx? pbc)
    {
        try
        {
            var hwnd = HWND.NULL;

            if (_site != null)
            {
                // Get the HWND of the browser from the site to parent our message box to
                IUnknown_QueryService(_site, SID_STopLevelBrowser, IID_IUnknown, out var browser).ThrowIfFailed();
                IUnknown_GetWindow(browser!, out hwnd);
            }

            for (uint i = 0; i < psiItemArray.GetCount(); i++)
            {
                using var pShellItem = ComReleaserFactory.Create(psiItemArray.GetItemAt(i));

                var rawFullPath = pShellItem.Item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH);
                logger.LogDebug("Sync Command received for file {path}", rawFullPath);

                commandWriter.TryWrite(new ShellCommand
                {
                    Kind = ShellCommandKind.Do,
                    FullPath = rawFullPath,
                });

                //var relativePath = client.GetRelativePath(rawFullPath);

                // TODO: placeholder service createorupdate - but how? 
            }
        }
        catch (Exception ex)
        {
            return ex.HResult;
        }

        return HRESULT.S_OK;
    }

    public HRESULT GetFlags(out EXPCMDFLAGS pFlags)
    {
        pFlags = EXPCMDFLAGS.ECF_DEFAULT;
        return HRESULT.S_OK;
    }

    public HRESULT EnumSubCommands(out IEnumExplorerCommand? ppEnum)
    {
        ppEnum = null;
        return HRESULT.E_NOTIMPL;
    }

    // IObjectWithSite
    private object? _site;
    public HRESULT SetSite(object? pUnkSite)
    {
        _site = pUnkSite;
        return HRESULT.S_OK;
    }

    public HRESULT GetSite(in Guid riid, out object? ppvSite)
    {
        if (_site is null)
        {
            ppvSite = null;
            return HRESULT.E_NOINTERFACE;
        }
        var myriid = riid;
        HRESULT hr = Marshal.QueryInterface(Marshal.GetIUnknownForObject(_site), ref myriid, out var ppv);
        ppvSite = hr.Succeeded ? Marshal.GetObjectForIUnknown(ppv) : null;
        return hr;
    }
}
