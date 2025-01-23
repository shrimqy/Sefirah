namespace Sefirah.App.RemoteStorage.RemoteAbstractions;
public interface IRemoteContextSetter
{
    string RemoteKind { get; }
    void SetRemoteContext(byte[] contextBytes);
}
