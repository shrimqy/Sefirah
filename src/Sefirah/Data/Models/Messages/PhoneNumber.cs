namespace Sefirah.Data.Models.Messages;

public class PhoneNumber(string number, int subscriptionId)
{
    public string Number { get; set; } = number;
    public int SubscriptionId { get; set; } = subscriptionId;
}
