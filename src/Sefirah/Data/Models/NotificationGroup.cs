namespace Sefirah.Data.Models;

public sealed class NotificationGroup(string sender, List<string> messages)
{
    public string Sender { get; set; } = sender;
    public List<string> Messages { get; set; } = messages;
}
