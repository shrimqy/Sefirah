using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Shell32;

namespace Sefirah.Platforms.Windows.Interop;

public static class RecycleBin
{
    public static void MoveToRecycleBin(string path) => MoveToRecycleBin([path]);

    /// <summary>Recycles all given paths in a single shell operation. Throws if the operation fails or is aborted.</summary>
    public static void MoveToRecycleBin(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Execute(paths);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }
    }

    private static void Execute(IReadOnlyCollection<string> paths)
    {
        var type = Type.GetTypeFromCLSID(typeof(CFileOperations).GUID)
            ?? throw new TypeLoadException("Could not resolve IFileOperation type");
        var fileOperation = (IFileOperation)(Activator.CreateInstance(type)
            ?? throw new NotSupportedException("Could not create IFileOperation instance"));

        try
        {
            fileOperation.SetOperationFlags(FILEOP_FLAGS.FOFX_RECYCLEONDELETE | FILEOP_FLAGS.FOF_NO_UI);
            var progressSink = new FileOperationProgressSink();

            foreach (var path in paths)
            {
                // SHCreateItemFromParsingName needs a canonical Windows path; remote-derived paths can contain
                // forward slashes, which it rejects with E_INVALIDARG. GetFullPath normalizes the separators.
                var fullPath = Path.GetFullPath(path);
                var item = SHCreateItemFromParsingName<IShellItem>(fullPath)
                    ?? throw new InvalidOperationException($"Could not create shell item for {fullPath}");

                try
                {
                    fileOperation.DeleteItem(item, progressSink);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(item);
                }
            }

            fileOperation.PerformOperations();
            if (fileOperation.GetAnyOperationsAborted())
            {
                throw new IOException("Recycle Bin operation was aborted");
            }

            progressSink.ThrowIfAnyDeletionFailed();
        }
        finally
        {
            Marshal.FinalReleaseComObject(fileOperation);
        }
    }

    /// <summary>Captures per-item delete results; PerformOperations alone does not surface individual failures.</summary>
    private sealed class FileOperationProgressSink : IFileOperationProgressSink
    {
        private readonly List<HRESULT> _failedDeletions = [];

        public void ThrowIfAnyDeletionFailed()
        {
            if (_failedDeletions.Count == 0)
            {
                return;
            }

            _failedDeletions[0].ThrowIfFailed("Recycle Bin delete failed");
        }

        HRESULT IFileOperationProgressSink.PostDeleteItem(
            TRANSFER_SOURCE_FLAGS dwFlags,
            IShellItem psiItem,
            HRESULT hrDelete,
            IShellItem? psiNewlyCreated)
        {
            if (hrDelete.Failed)
            {
                _failedDeletions.Add(hrDelete);
            }

            return HRESULT.S_OK;
        }

        HRESULT IFileOperationProgressSink.StartOperations() => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.FinishOperations(HRESULT hrResult) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PreRenameItem(TRANSFER_SOURCE_FLAGS dwFlags, IShellItem psiItem, string pszNewName) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PostRenameItem(
            TRANSFER_SOURCE_FLAGS dwFlags,
            IShellItem psiItem,
            string pszNewName,
            HRESULT hrRename,
            IShellItem psiNewlyCreated) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PreMoveItem(
            TRANSFER_SOURCE_FLAGS dwFlags,
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            string? pszNewName) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PostMoveItem(
            TRANSFER_SOURCE_FLAGS dwFlags,
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            string pszNewName,
            HRESULT hrMove,
            IShellItem psiNewlyCreated) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PreCopyItem(
            TRANSFER_SOURCE_FLAGS dwFlags,
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            string? pszNewName) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PostCopyItem(
            TRANSFER_SOURCE_FLAGS dwFlags,
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            string pszNewName,
            HRESULT hrCopy,
            IShellItem psiNewlyCreated) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PreDeleteItem(TRANSFER_SOURCE_FLAGS dwFlags, IShellItem psiItem) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PreNewItem(TRANSFER_SOURCE_FLAGS dwFlags, IShellItem psiDestinationFolder, string pszNewName) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PostNewItem(
            TRANSFER_SOURCE_FLAGS dwFlags,
            IShellItem psiDestinationFolder,
            string pszNewName,
            string? pszTemplateName,
            uint dwFileAttributes,
            HRESULT hrNew,
            IShellItem psiNewItem) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.UpdateProgress(uint iWorkTotal, uint iWorkSoFar) => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.ResetTimer() => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.PauseTimer() => HRESULT.S_OK;
        HRESULT IFileOperationProgressSink.ResumeTimer() => HRESULT.S_OK;
    }
}
