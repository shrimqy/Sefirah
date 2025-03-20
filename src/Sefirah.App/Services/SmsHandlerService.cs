using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using Sefirah.App.Utils.Serialization;

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
                            AddNewConversation(textConversation);
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
                            AddNewConversation(textConversation);
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
                        logger.Info("Adding new conversation: {0}", textConversation.ThreadId);
                        AddNewConversation(textConversation);
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

    public void AddNewConversation(TextConversation textConversation)
    {
        var newConv = new SmsConversation(textConversation);
        int index = FindInsertionIndex(newConv);
        Conversations.Insert(index, newConv);
    }

    private int FindInsertionIndex(SmsConversation conversation)
    {
        for (int i = 0; i < Conversations.Count; i++)
        {
            // Compare timestamps - if current conversation is older than the new one, insert here
            if (Conversations[i].LastMessageTimestamp < conversation.LastMessageTimestamp)
            {
                return i;
            }
        }

        // If we get here, this is the oldest conversation or collection is empty
        return Conversations.Count;
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
