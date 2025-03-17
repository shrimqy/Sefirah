using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;

public interface ISmsHandlerService
{
    ObservableCollection<SmsConversation> Conversations { get; }
    Task HandleTextMessage(TextConversation textConversation);
    Task SendTextMessage(TextMessage textMessage);
    Task RequestThreadHistory(long threadId, long rangeStartTimestamp = -1, long numberToRequest = -1);
}
