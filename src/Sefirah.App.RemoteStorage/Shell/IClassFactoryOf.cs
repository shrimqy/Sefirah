using static Vanara.PInvoke.Ole32;

namespace Sefirah.App.RemoteStorage.Shell;
public interface IClassFactoryOf : IClassFactory
{
    Type Type { get; }
}
