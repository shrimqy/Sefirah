namespace Sefirah.Data.Models.Messages;

public partial class MessageGroup(Contact sender) : ObservableObject
{
    public Contact Sender { get; } = sender;

    public ObservableCollection<Message> Messages { get; set; } = [];

    public long LatestTimestamp => Messages.Count > 0 ? Messages[^1].Timestamp : 0;

    public bool IsReceived => Messages.Count > 0 && Messages[0].MessageType == 1;
}
