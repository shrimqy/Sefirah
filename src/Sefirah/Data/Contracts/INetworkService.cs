namespace Sefirah.Data.Contracts;
interface INetworkService
{
    Task<bool> StartServerAsync();
}
