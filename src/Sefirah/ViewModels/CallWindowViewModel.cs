namespace Sefirah.ViewModels;

public sealed partial class CallWindowViewModel : BaseViewModel, IDisposable
{
    private readonly ICallFeature callFeature = Ioc.Default.GetRequiredService<ICallFeature>();
    private bool disposed;

    public CallSessionViewModel? PrimaryCall => callFeature.PrimaryCall;

    public CallSessionViewModel? SecondaryCall => callFeature.SecondaryCall;

    public CallWindowViewModel()
    {
        callFeature.ActiveCallChanged += OnActiveCallChanged;
    }

    private void OnActiveCallChanged(object? sender, EventArgs e) 
    {
        OnPropertyChanged(nameof(PrimaryCall));
        OnPropertyChanged(nameof(SecondaryCall));
    }


    [RelayCommand]
    private Task SwapCallsAsync() => callFeature.SwapCallsAsync();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        callFeature.ActiveCallChanged -= OnActiveCallChanged;
    }
}
