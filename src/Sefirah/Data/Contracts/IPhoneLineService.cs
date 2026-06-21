namespace Sefirah.Data.Contracts;

public interface IPhoneLineService
{
    CallingLineStatus LineStatus { get; }

    event EventHandler<CallingLineStatus>? LineStatusChanged;

    event EventHandler<IPhoneCall>? CallStateChanged;

    Task Initialize();

    Task RefreshStateAsync(CancellationToken cancellationToken = default);

    Task DialAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
