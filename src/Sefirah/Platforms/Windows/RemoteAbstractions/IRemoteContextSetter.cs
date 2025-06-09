namespace Sefirah.Platforms.Windows.RemoteAbstractions;
public interface IRemoteContextSetter
{
    string RemoteKind { get; }
    void SetRemoteContext(byte[] contextBytes);
}
