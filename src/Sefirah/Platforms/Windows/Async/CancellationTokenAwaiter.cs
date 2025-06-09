using System.Runtime.CompilerServices;

namespace Sefirah.Platforms.Windows.Async;
public struct CancellationTokenAwaiter(CancellationToken cancellationToken) : INotifyCompletion, ICriticalNotifyCompletion
{
    public void GetResult() { }

    // called by compiler generated/.net internals to check
    // if the task has completed.
    public bool IsCompleted => cancellationToken.IsCancellationRequested;

    // The compiler will generate stuff that hooks in
    // here. We hook those methods directly into the
    // cancellation token.
    public void OnCompleted(Action continuation) =>
            cancellationToken.Register(continuation);
    public void UnsafeOnCompleted(Action continuation) =>
            cancellationToken.Register(continuation);
}
