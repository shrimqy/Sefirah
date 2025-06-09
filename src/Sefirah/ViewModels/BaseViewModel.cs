namespace Sefirah.ViewModels;
public abstract class BaseViewModel : ObservableObject
{
    public Microsoft.UI.Dispatching.DispatcherQueue dispatcher;
    protected ILogger Logger { get; }

    protected BaseViewModel()
    {
        dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Logger = Ioc.Default.GetRequiredService<ILogger>();
    }
}
