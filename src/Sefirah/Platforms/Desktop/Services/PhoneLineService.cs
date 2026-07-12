namespace Sefirah.Platforms.Desktop.Services;

public sealed class PhoneLineService : IPhoneLineService
{
    public CallingLineStatus LineStatus => CallingLineStatus.NotSupported;

    public event EventHandler<CallingLineStatus>? LineStatusChanged;

    public event EventHandler<IPhoneCall>? CallStateChanged;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task RefreshStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DialAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
