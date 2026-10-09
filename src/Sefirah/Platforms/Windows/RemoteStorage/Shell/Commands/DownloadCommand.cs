using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Channels;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.Shell32;
using FileAttributes = System.IO.FileAttributes;

namespace Sefirah.Platforms.Windows.RemoteStorage.Shell.Commands;

[ComVisible(true), Guid("9e2e55ac-bcb2-432f-b29a-d2c6fbbc244b")]
public class DownloadCommand(
    ChannelWriter<ShellCommand> commandWriter,
    ILogger logger) : IExplorerCommand, IExplorerCommandState, IObjectWithSite
{
    public HRESULT GetTitle(IShellItemArray psiItemArray, out string? ppszName)
    {
        ppszName = "CloudFilesContextMenu.KeepOnDevice".GetLocalizedResource();
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

    /// <summary>Shown when the selection has an online-only file or a folder.</summary>
    public HRESULT GetState(IShellItemArray psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        pCmdState = EXPCMDSTATE.ECS_HIDDEN;
        try
        {
            var count = psiItemArray?.GetCount() ?? 0;
            if (count == 0)
            {
                return HRESULT.S_OK;
            }
            // Defer large selections to the slow call
            if (!fOkToBeSlow && count > 16)
            {
                return HRESULT.E_PENDING;
            }

            for (uint i = 0; i < count; i++)
            {
                using var pShellItem = ComReleaserFactory.Create(psiItemArray!.GetItemAt(i));
                var attributes = File.GetAttributes(pShellItem.Item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH));
                if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.Offline))
                {
                    pCmdState = EXPCMDSTATE.ECS_ENABLED;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to get download command state", ex);
        }
        return HRESULT.S_OK;
    }

    public HRESULT Invoke(IShellItemArray psiItemArray, IBindCtx? pbc)
    {
        try
        {
            for (uint i = 0; i < psiItemArray.GetCount(); i++)
            {
                using var pShellItem = ComReleaserFactory.Create(psiItemArray.GetItemAt(i));

                var rawFullPath = pShellItem.Item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH);
                logger.Debug($"Download Command received for {rawFullPath}");

                commandWriter.TryWrite(new ShellCommand
                {
                    Kind = ShellCommandKind.Download,
                    FullPath = rawFullPath,
                });
            }
        }
        catch (Exception ex)
        {
            logger.Error("Download command failed", ex);
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
