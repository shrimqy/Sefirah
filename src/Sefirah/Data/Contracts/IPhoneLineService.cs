namespace Sefirah.Data.Contracts;

public interface IPhoneLineService
{
    CallingLineStatus LineStatus { get; }

    event EventHandler<CallingLineStatus>? LineStatusChanged;

    event EventHandler<IPhoneCall>? CallStateChanged;

    Task InitializeAsync();

    Task RefreshStateAsync(CancellationToken cancellationToken = default);

    Task DialAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
