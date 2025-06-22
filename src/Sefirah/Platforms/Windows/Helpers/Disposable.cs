namespace Sefirah.Platforms.Windows.Helpers;
public class Disposable(Action dispose) : IDisposable
{
    private bool _disposed = false;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
