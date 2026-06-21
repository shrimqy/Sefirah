using Sefirah.ViewModels;

namespace Sefirah.Services;

public interface ICallManager
{
    Task Initialize();

    CallSessionViewModel? PrimaryCall { get; }

    CallSessionViewModel? SecondaryCall { get; }

    event EventHandler? ActiveCallChanged;

    Task SwapCallsAsync();
}
