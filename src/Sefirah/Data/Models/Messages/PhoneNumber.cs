namespace Sefirah.Data.Models.Messages;

public class PhoneNumber(string number, int subscriptionId)
{
    public string Number { get; set; } = number;

    public int SubscriptionId { get; set; } = subscriptionId;

    public int Index { get; set; }

    #region Helpers
    [JsonIgnore]
    public string Glyph => Index == 1 ? "\uE882" : "\uE884";
    #endregion
}
