using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Channels;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.ShlwApi;

namespace Sefirah.Platforms.Windows.RemoteStorage.Shell.Commands;
[ComVisible(true), Guid("4a3c9b56-f075-4499-b4ee-ba4b88d1fe05")]
public class UploadCommand(
    ChannelWriter<ShellCommand> commandWriter, 
    ILogger logger) : IExplorerCommand, IExplorerCommandState, IObjectWithSite
{
    public HRESULT GetTitle(IShellItemArray psiItemArray, out string? ppszName)
    {
        ppszName = "Reupload To Cloud";
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
                logger.LogInformation("Upload Command received for file {path}", rawFullPath);

                // Clear the in-sync flag to force reupload
                try
                {
                    var currentState = CloudFilter.GetPlaceholderState(rawFullPath);
                    logger.LogInformation("Current state before reupload: {state}", currentState);

                    if (currentState.HasFlag(CF_PLACEHOLDER_STATE.CF_PLACEHOLDER_STATE_IN_SYNC))
                    {
                        CloudFilter.ClearInSyncState(rawFullPath);
                        logger.LogInformation("Cleared in-sync state for reupload");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to clear sync state for {path}", rawFullPath);
                }

                commandWriter.TryWrite(new ShellCommand
                {
                    Kind = ShellCommandKind.Do,
                    FullPath = rawFullPath,
                });
                //client.StartUpload(rawFullPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload command failed");
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
