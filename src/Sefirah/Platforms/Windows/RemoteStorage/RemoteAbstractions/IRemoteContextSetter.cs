namespace Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
public interface IRemoteContextSetter
{
    string RemoteKind { get; }
    void SetRemoteContext(byte[] contextBytes);
}
