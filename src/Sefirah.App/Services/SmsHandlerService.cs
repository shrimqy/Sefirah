using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using Sefirah.App.Utils.Serialization;
using System.Threading;

namespace Sefirah.App.Services;
public class SmsHandlerService(
    ILogger logger,
    ISessionManager sessionManager
) : ISmsHandlerService
{
    public ObservableCollection<SmsConversation> Conversations { get; } = [];
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly DispatcherQueue dispatcher = MainWindow.Instance.DispatcherQueue;

    public async Task HandleTextMessage(TextConversation textConversation)
    {
        logger.Info("Received text conversation: {0} with type: {1}", textConversation.ThreadId, textConversation.ConversationType);
        
        await semaphore.WaitAsync();
        try
        {
            await dispatcher.EnqueueAsync(() =>
            {
                SmsConversation? existingConversation = null;
                
                switch (textConversation.ConversationType) 
                {
                    case ConversationType.Active:
                        existingConversation = Conversations.FirstOrDefault(m => m.ThreadId == textConversation.ThreadId);
                        if (existingConversation != null)
                        {
                            logger.Info("Updating existing conversation: {0}", existingConversation.ThreadId);
                            existingConversation.UpdateFromTextConversation(textConversation);
                        }
                        else
                        {
                            logger.Info("Active conversation not found, creating new: {0}", textConversation.ThreadId);
                            var newConversation = new SmsConversation(textConversation);
                            Conversations.Insert(0, newConversation);
                        }
                        break;
                    case ConversationType.ActiveUpdated:
                        existingConversation = Conversations.FirstOrDefault(m => m.ThreadId == textConversation.ThreadId);
                        if (existingConversation != null)
                        {
                            logger.Info("Updating existing conversation: {0}", existingConversation.ThreadId);
                            existingConversation.NewMessageFromConversation(textConversation);
                        }
                        else
                        {
                            logger.Info("Active conversation not found, creating new: {0}", textConversation.ThreadId);
                            var newConversation = new SmsConversation(textConversation);
                            Conversations.Insert(0, newConversation);
                        }
                        break;

                    case ConversationType.Removed:
                        existingConversation = Conversations.FirstOrDefault(m => m.ThreadId == textConversation.ThreadId);
                        if (existingConversation != null)
                        {
                            logger.Info("Removing conversation: {0}", existingConversation.ThreadId);
                            Conversations.Remove(existingConversation);
                        }
                        break;
                    case ConversationType.New:
                        var newConv = new SmsConversation(textConversation);
                        logger.Info("Adding new conversation: {0}", textConversation.ThreadId);
                        Conversations.Insert(0, newConv);
                        break;
                    default:
                        logger.Warn("Unknown conversation type: {0}", textConversation.ConversationType);
                        break;
                }
            });
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task RequestThreadHistory(long threadId, long rangeStartTimestamp = -1, long numberToRequest = -1)
    {
        var threadRequest = new ThreadRequest
        {
            ThreadId = threadId,
            RangeStartTimestamp = rangeStartTimestamp,
            NumberToRequest = numberToRequest
        };
        sessionManager.SendMessage(SocketMessageSerializer.Serialize(threadRequest));
    }

    public async Task SendTextMessage(TextMessage textMessage)
    {
        logger.Info("Sending text message: {0} to {1}", textMessage.Body, textMessage.Addresses[0].Address);
        sessionManager.SendMessage(SocketMessageSerializer.Serialize(textMessage));
    }
}
