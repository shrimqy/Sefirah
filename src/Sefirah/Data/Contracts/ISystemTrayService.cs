namespace Sefirah.Data.Contracts;

public interface ISystemTrayService : IDisposable
{
    bool IsAvailable { get; }
}
