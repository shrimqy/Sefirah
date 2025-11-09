namespace Sefirah.Data.Contracts;

public interface INetworkService
{
    Task<bool> StartServerAsync();
    int ServerPort { get; }
}
