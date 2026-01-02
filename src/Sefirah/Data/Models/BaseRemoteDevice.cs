using System.Text;
using Sefirah.Services.Socket;
using Sefirah.Utils.Serialization;

namespace Sefirah.Data.Models;

public abstract partial class BaseRemoteDevice : ObservableObject
{
    private string id = string.Empty;
    public string Id
    {
        get => id;
        set => SetProperty(ref id, value);
    }

    private string name = string.Empty;
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    private string model = string.Empty;
    public string Model
    {
        get => model;
        set => SetProperty(ref model, value);
    }

    public string Address { get; set; } = string.Empty;

    private ServerSession? session;
    public ServerSession? Session
    {
        get => session;
        set => SetProperty(ref session, value);
    }

    private Client? client;
    public Client? Client
    {
        get => client;
        set => SetProperty(ref client, value);
    }

    public virtual void SendMessage(SocketMessage message)
    {
        try
        {
            var stringMessage = JsonMessageSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(stringMessage + "\n");
            Session?.SendAsync(messageBytes);
            Client?.SendAsync(messageBytes);
        }
        catch { }
    }
}
