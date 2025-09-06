using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Sftp;
public interface ISftpContextAccessor
{
    SftpContext Context { get; }
}

public class SftpContextAccessor : IRemoteContextSetter, ISftpContextAccessor
{
    private static readonly AsyncLocal<ContextHolder> _sftpContextCurrent = new();

    public string RemoteKind => SftpConstants.KIND;

    /// <inheritdoc/>
    public SftpContext Context
    {
        get => _sftpContextCurrent.Value?.Context! ?? throw new NullReferenceException();
        set
        {
            var holder = _sftpContextCurrent.Value;
            // Clear current SftpContext trapped in the AsyncLocals, as its done.
            holder?.Context = null;

            // Use an object indirection to hold the SftpContext in the AsyncLocal,
            // so it can be cleared in all ExecutionContexts when its cleared.
            _sftpContextCurrent.Value = new ContextHolder { Context = value };
        }
    }

    public void SetRemoteContext(byte[] contextBytes)
    {
        Context = StructBytes.FromBytes<SftpContext>(contextBytes);
    }

    private sealed class ContextHolder
    {
        public SftpContext? Context;
    }
}
