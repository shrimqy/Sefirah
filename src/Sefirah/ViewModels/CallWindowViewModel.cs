using Sefirah.Services;

namespace Sefirah.ViewModels;

public sealed partial class CallWindowViewModel : BaseViewModel, IDisposable
{
    private readonly ICallManager callManager = Ioc.Default.GetRequiredService<ICallManager>();
    private bool disposed;

    public CallSessionViewModel? PrimaryCall => callManager.PrimaryCall;

    public CallSessionViewModel? SecondaryCall => callManager.SecondaryCall;

    public CallWindowViewModel()
    {
        callManager.ActiveCallChanged += OnActiveCallChanged;
    }

    private void OnActiveCallChanged(object? sender, EventArgs e) 
    {
        OnPropertyChanged(nameof(PrimaryCall));
        OnPropertyChanged(nameof(SecondaryCall));
    }


    [RelayCommand]
    private Task SwapCallsAsync() => callManager.SwapCallsAsync();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        callManager.ActiveCallChanged -= OnActiveCallChanged;
    }
}
