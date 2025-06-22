using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;

namespace Sefirah.Platforms.Windows.RemoteStorage.Shell;
public class ClassFactory<T>(ClassFactory<T>.Generator generator) : IClassFactoryOf
{
    public Type Type => typeof(T);

    public HRESULT CreateInstance(object? pUnkOuter, in Guid riid, out object? ppvObject)
    {
        if (pUnkOuter != null)
        {
            ppvObject = null;
            return HRESULT.CLASS_E_NOAGGREGATION;
        }
        if (riid != IID_IUnknown)
        {
            // We cannot handle this for now
            ppvObject = null;
            return HRESULT.E_NOINTERFACE;
        }
        else
        {
            ppvObject = generator();
            return HRESULT.S_OK;
        }
    }

    public HRESULT LockServer(bool fLock) => HRESULT.S_OK;

    public delegate T Generator();
}
