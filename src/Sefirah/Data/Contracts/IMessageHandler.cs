using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;
public interface IMessageHandler
{
    void HandleMessageAsync(PairedDevice device, SocketMessage message);
}
