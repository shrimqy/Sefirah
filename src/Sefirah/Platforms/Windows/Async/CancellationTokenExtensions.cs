namespace Sefirah.Platforms.Windows.Async;
public static class CancellationTokenExtensions
{
    public static CancellationTokenAwaiter GetAwaiter(this CancellationToken cancellationToken) =>
        new(cancellationToken);
}
