namespace Sefirah.App.ViewModels;
public abstract class BaseViewModel : ObservableObject
{
    protected readonly ILogger logger;

    // Properties
    public Microsoft.UI.Dispatching.DispatcherQueue dispatcher;

    protected BaseViewModel()
    {
        dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        logger = Ioc.Default.GetRequiredService<ILogger>();
    }
}
