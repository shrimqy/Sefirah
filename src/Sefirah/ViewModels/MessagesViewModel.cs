using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Utils;

namespace Sefirah.ViewModels;
public sealed partial class MessagesViewModel : BaseViewModel
{
    #region Services
    private readonly ISmsFeature smsFeature = Ioc.Default.GetRequiredService<ISmsFeature>();
    private readonly ContactRepository contactRepository = Ioc.Default.GetRequiredService<ContactRepository>();
    private readonly ISessionManager sessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
    private readonly IDeviceManager deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    #endregion

    #region Properties
    public ObservableCollection<Conversation> Conversations { get; } = [];
    private HashSet<long> MessageIds { get; set; } = [];
    private bool loadingOlder;
    private bool reachedCacheStart;

    private ObservableCollection<MessageGroup> messageGroups = [];
    public ObservableCollection<MessageGroup> MessageGroups
    {
        get => messageGroups;
        set => SetProperty(ref messageGroups, value);
    }

    private Conversation? selectedConversation;
    public Conversation? SelectedConversation
    {
        get => selectedConversation;
        set
        {
            // If selecting a conversation, exit new conversation mode 
            if (value is not null)
            {
                IsNewConversation = false;
            }

            if (SetProperty(ref selectedConversation, value))
            {
                LoadMessagesForSelectedConversation();
                OnPropertyChanged(nameof(ShouldShowComposeUI));
                OnPropertyChanged(nameof(ShouldShowEmptyState));
            }
        }
    }

    [ObservableProperty]
    public partial bool IsNewConversation { get; set; }

    public ObservableCollection<Contact> NewConversationRecipients { get; } = [];

    public ObservableCollection<StagedAttachment> StagedAttachments { get; } = [];

    public bool HasStagedAttachments => StagedAttachments.Count > 0;

    public bool CanSend => !string.IsNullOrWhiteSpace(MessageText) || HasStagedAttachments;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PhoneNumber? SelectedPhoneNumber { get; set; } = null;

    public bool ShowSimPicker => (ActiveDevice?.PhoneNumbers.Count ?? 0) > 1;

    public PairedDevice? ActiveDevice => deviceManager.ActiveDevice;

    private static long nextTempId;

    public bool ShouldShowEmptyState => !IsNewConversation && SelectedConversation is null;
    public bool ShouldShowComposeUI => IsNewConversation || SelectedConversation is not null;

    partial void OnIsNewConversationChanged(bool value)
    {
        OnPropertyChanged(nameof(ShouldShowComposeUI));
        OnPropertyChanged(nameof(ShouldShowEmptyState));
    }

    partial void OnMessageTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanSend));
    }
    
    #endregion

    public MessagesViewModel()
    {
        deviceManager.ActiveDeviceChanged += OnActiveDeviceChanged;
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        smsFeature.ConversationRemoved += OnConversationRemoved;
        smsFeature.ConversationUpdated += OnConversationUpdated;
        smsFeature.QueuedMessagesSent += OnQueuedMessagesSent;
        StagedAttachments.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasStagedAttachments));
            OnPropertyChanged(nameof(CanSend));
        };
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        try
        {
            if (ActiveDevice is not null)
            {
                await LoadConversationsForActiveDevice();
                await dispatcher.EnqueueAsync(SyncSelectedSim);
            }
            else
            {
                await dispatcher.EnqueueAsync(() => Conversations.Clear());
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error initializing messages for device: {ActiveDevice?.Id}", ex);
        }
    }

    private void OnActiveDeviceChanged(object? sender, PairedDevice? _)
    {
        OnPropertyChanged(nameof(ActiveDevice));
        InitializeAsync();
        SelectedConversation = null;
        IsNewConversation = false;
        StagedAttachments.Clear();
        SyncSelectedSim();
    }

    private void OnQueuedMessagesSent(object? sender, string deviceId)
    {
        if (ActiveDevice?.Id != deviceId) return;
        dispatcher.EnqueueAsync(RefreshSendIndicators);
    }

    private async Task LoadConversationsForActiveDevice()
    {
        try
        {
            var conversations = await smsFeature.LoadConversationAsync(ActiveDevice!.Id);
            await dispatcher.EnqueueAsync(() =>
            {
                Conversations.Clear();
                Conversations.AddRange(conversations);
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading conversations for device: {ActiveDevice?.Id}", ex);
        }
    }

    private async void LoadMessagesForSelectedConversation()
    {
        reachedCacheStart = false;
        MessageGroups.Clear();
        MessageIds.Clear();
        if (SelectedConversation is not { } conversation || ActiveDevice is not { } device)
            return;

        await LoadLatestMessages();
        if (!ReferenceEquals(SelectedConversation, conversation) || ActiveDevice?.Id != device.Id)
            return;

        AppendQueuedToGroups(device.Id, conversation.ThreadId);
        RequestSelectedThread();
    }

    private async Task LoadLatestMessages()
    {
        if (SelectedConversation is not { IsTemporaryThread: false } conversation || ActiveDevice is not { } device)
            return;

        try
        {
            var messages = await smsFeature.LoadMessagesForConversation(device.Id, conversation.ThreadId);
            if (!ReferenceEquals(SelectedConversation, conversation) || ActiveDevice?.Id != device.Id)
                return;

            reachedCacheStart = messages.Count == 0;
            AddMessages(messages);
            AppendQueuedToGroups(device.Id, conversation.ThreadId);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading messages for conversation {conversation.ThreadId}", ex);
        }
    }

    public async Task LoadOlderMessages()
    {
        if (loadingOlder || MessageGroups.Count == 0 ||
            SelectedConversation is not { IsTemporaryThread: false } conversation || ActiveDevice is not { } device)
            return;

        if (device.IsConnected
            ? conversation.MessageIndex?.IsSubsetOf(MessageIds) == true
            : reachedCacheStart)
            return;

        var oldest = MessageGroups
            .SelectMany(g => g.Messages)
            .Where(m => m.UniqueId >= 0)
            .OrderBy(m => m.Timestamp)
            .ThenBy(m => m.UniqueId)
            .FirstOrDefault();
        if (oldest is null)
            return;

        loadingOlder = true;
        try
        {
            var messages = await smsFeature.LoadOlderMessagesAsync(
                device, conversation.ThreadId, oldest, MessageIds, conversation.MessageIndex);
            if (!ReferenceEquals(SelectedConversation, conversation) || ActiveDevice?.Id != device.Id)
                return;

            if (messages.Count == 0)
            {
                reachedCacheStart = !device.IsConnected;
                return;
            }

            foreach (var message in messages)
                if (MessageIds.Add(message.UniqueId))
                    MessageGrouping.AddMessage(MessageGroups, message);
            RefreshMessagePresentation();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading older messages for conversation {conversation.ThreadId}", ex);
        }
        finally
        {
            loadingOlder = false;
        }
    }

    private void AppendQueuedToGroups(string deviceId, long threadId)
    {
        var queued = smsFeature.GetQueuedMessages(deviceId, threadId);
        foreach (var message in queued)
        {
            if (MessageIds.Add(message.UniqueId))
                MessageGrouping.AddMessage(MessageGroups, message);
        }

        if (queued.Count > 0)
            RefreshMessagePresentation();
    }

    private void RequestSelectedThread()
    {
        if (SelectedConversation is not { IsTemporaryThread: false } conversation ||
            ActiveDevice is not { IsConnected: true } device)
            return;
        try { smsFeature.RequestLatestMessages(device, conversation.ThreadId); }
        catch (Exception ex) { Logger.Error($"Error requesting thread {conversation.ThreadId}", ex); }
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (ActiveDevice?.Id != device.Id) return;
        dispatcher.EnqueueAsync(() =>
        {
            if (ActiveDevice?.Id != device.Id) return;
            reachedCacheStart = false;
            foreach (var conversation in Conversations)
                conversation.MessageIndex = null;
            if (device.IsConnected)
                RequestSelectedThread();
        });
    }

    private const long MaxOutgoingAttachmentBytes = 5 * 1024 * 1024;

    public async Task SendMessage(string messageText)
    {
        var hasText = !string.IsNullOrWhiteSpace(messageText);
        var hasAttachments = StagedAttachments.Count > 0;
        if ((!hasText && !hasAttachments) || ActiveDevice is null) return;
        
        try
        {
            List<Contact> recipientContacts;
            Conversation conversation;
            if (IsNewConversation)
            {
                recipientContacts = [.. NewConversationRecipients];
                if (recipientContacts.Count == 0) return;

                var tempId = NextTempId();
                conversation = new Conversation
                {
                    ConversationKey = $"queued:{ActiveDevice.Id}:{tempId}",
                    ThreadId = tempId,
                    Contacts = recipientContacts,
                };
                Conversations.Insert(0, conversation);
            }
            else if (SelectedConversation is not null)
            {
                conversation = SelectedConversation;
                recipientContacts = conversation.Contacts;
                if (recipientContacts.Count == 0) return;
            }
            else
            {
                return;
            }

            var recipients = recipientContacts.Select(c => c.Address).ToList();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var uniqueId = NextTempId();
            var body = (messageText ?? string.Empty).Trim();
            var participant = recipientContacts[0];
            var deviceId = ActiveDevice.Id;

            List<MessageAttachment> uiAttachments = [];
            if (hasAttachments)
            {
                for (var partId = 0; partId < StagedAttachments.Count; partId++)
                {
                    var staged = StagedAttachments[partId];
                    await AttachmentStorage.CopyFromFileAsync(
                        staged.File, deviceId, uniqueId, partId, staged.FileName, staged.MimeType);
                    var path = AttachmentStorage.GetPathIfExists(
                        deviceId, uniqueId, partId, staged.FileName, staged.MimeType);
                    uiAttachments.Add(new MessageAttachment
                    {
                        FileName = staged.FileName,
                        MimeType = staged.MimeType,
                        PartId = partId,
                        FilePath = path,
                    });
                }
            }

            var uiMessage = new Message
            {
                MessageKey = $"queued:{uniqueId}",
                UniqueId = uniqueId,
                ThreadId = conversation.ThreadId,
                Body = body,
                Timestamp = timestamp,
                MessageType = 2,
                Read = true,
                SubscriptionId = SelectedPhoneNumber?.SubscriptionId ?? -1,
                Participant = participant,
                Attachments = uiAttachments,
            };

            smsFeature.QueueSendingMessage(ActiveDevice, uiMessage, recipients);

            conversation.LastMessage = Message.PreviewText(body, uiAttachments.Select(a => a.MimeType));
            conversation.LastMessageTimestamp = timestamp;
            conversation.HasRead = true;

            var currentIndex = Conversations.IndexOf(conversation);
            if (currentIndex > 0)
                Conversations.Move(currentIndex, 0);

            MessageText = string.Empty;
            StagedAttachments.Clear();

            if (IsNewConversation || !ReferenceEquals(SelectedConversation, conversation))
            {
                NewConversationRecipients.Clear();
                SelectedConversation = conversation;
            }
            else if (MessageIds.Add(uiMessage.UniqueId))
            {
                MessageGrouping.AddMessage(MessageGroups, uiMessage);
                RefreshMessagePresentation();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error sending message", ex);
        }
    }

    private static long NextTempId() => Interlocked.Decrement(ref nextTempId);

    public async Task AddFiles(IReadOnlyList<StorageFile> files)
    {
        foreach (var file in files)
        {
            try
            {
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > MaxOutgoingAttachmentBytes)
                {
                    Logger.Warn($"Skipping attachment {file.Name}: exceeds {MaxOutgoingAttachmentBytes} bytes");
                    continue;
                }

                var mime = MimeFromFile(file);
                StagedAttachments.Add(await StagedAttachment.CreateAsync(file, mime));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error adding attachment {file.Name}", ex);
            }
        }
    }

    public void RemoveStagedAttachment(StagedAttachment attachment)
    {
        StagedAttachments.Remove(attachment);
    }

    private void SyncSelectedSim()
    {
        var numbers = ActiveDevice?.PhoneNumbers ?? [];
        if (numbers.Count == 0)
        {
            SelectedPhoneNumber = null;
        }
        else if (SelectedPhoneNumber is null ||
                 numbers.All(n => n.SubscriptionId != SelectedPhoneNumber.SubscriptionId))
        {
            SelectedPhoneNumber = numbers[0];
        }

        OnPropertyChanged(nameof(ShowSimPicker));
    }

    /// <summary>Download a GIF part in the background so the bubble can animate (see MessageAttachment.ImagePath).</summary>
    public Task PrefetchGifAsync(MessageAttachment attachment)
    {
        if (!attachment.IsGif || attachment.HasFullFile || attachment.IsLoading)
            return Task.CompletedTask;
        return GetAttachmentFileAsync(attachment);
    }

    public async Task OpenAttachment(MessageAttachment attachment)
    {
        var path = await GetAttachmentFileAsync(attachment);
        if (!string.IsNullOrEmpty(path))
            OpenFile(path);
        else if (attachment.IsImage && attachment.HasPreview)
            OpenFile(attachment.ImagePath);
    }

    public async Task OpenAttachmentWith(MessageAttachment attachment)
    {
        var path = await GetAttachmentFileAsync(attachment);
        if (!string.IsNullOrEmpty(path))
            OpenFileWith(path);
    }

    public async Task ShowAttachmentInFolder(MessageAttachment attachment)
    {
        var path = await GetAttachmentFileAsync(attachment);
        if (!string.IsNullOrEmpty(path))
            ShowInFolder(path);
    }

    private async Task<string?> GetAttachmentFileAsync(MessageAttachment attachment)
    {
        if (attachment.HasFullFile)
            return attachment.FilePath;

        if (ActiveDevice is not { } device)
            return null;

        attachment.IsLoading = true;
        try
        {
            var path = await smsFeature.GetAttachmentFileAsync(device, attachment.PartId);
            if (path is not null)
            {
                attachment.FilePath = path;
                if (ActiveDevice?.Id == device.Id)
                {
                    foreach (var current in MessageGroups.SelectMany(g => g.Messages).SelectMany(m => m.Attachments))
                        if (current.PartId == attachment.PartId)
                            current.FilePath = path;
                }
            }
            return path;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading attachment {attachment.PartId}", ex);
            return null;
        }
        finally
        {
            attachment.IsLoading = false;
        }
    }

    private void OpenFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open attachment {path}", ex);
        }
    }

    private void OpenFileWith(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,OpenAs_RunDLL \"{path}\"",
                    UseShellExecute = true
                });
                return;
            }

            OpenFile(path);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open attachment with picker {path}", ex);
        }
    }

    private void ShowInFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
                return;
            }

            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to show attachment in folder {path}", ex);
        }
    }

    private static string MimeFromFile(StorageFile file)
    {
        if (!string.IsNullOrEmpty(file.ContentType) &&
            !file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return file.ContentType;
        }

        return file.FileType.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".mp4" => "video/mp4",
            ".3gp" => "video/3gpp",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            _ => string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType
        };
    }
    public void StartNewConversation()
    {
        IsNewConversation = true;
        SelectedConversation = null;
        NewConversationRecipients.Clear();
        MessageText = string.Empty;
        StagedAttachments.Clear();
    }

    public void AddAddress(Contact contact)
    {
        NewConversationRecipients.Add(contact);
    }

    public void RemoveAddress(Contact contact)
    {
        NewConversationRecipients.Remove(contact);
    }

    [RelayCommand]
    public async Task RefreshConversations()
    {
        await LoadConversationsForActiveDevice();
    }

    public List<Conversation> SearchConversations(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2) return [];

        return Conversations
            .Where(c => c.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       c.LastMessage.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       c.Contacts.Any(s => s.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(c => c.LastMessageTimestamp)
            .Take(10)
            .ToList();
    }

    public List<Contact> SearchContacts(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText) || ActiveDevice is null) return [];
        return contactRepository.SearchContacts(ActiveDevice.Id, searchText);
    }

    private void OnConversationRemoved(object? sender, (string DeviceId, long ThreadId) args)
    {
        if (ActiveDevice?.Id != args.DeviceId) return;

        dispatcher.EnqueueAsync(() =>
        {
            var toRemove = Conversations.FirstOrDefault(c => c.ThreadId == args.ThreadId);
            if (toRemove is not null)
                Conversations.Remove(toRemove);
            if (SelectedConversation?.ThreadId == args.ThreadId)
            {
                MessageGroups.Clear();
                MessageIds.Clear();
                SelectedConversation = null;
                OnPropertyChanged(nameof(ShouldShowEmptyState));
                OnPropertyChanged(nameof(ShouldShowComposeUI));
            }
        });
    }

    private void OnConversationUpdated(object? sender, ConversationUpdate update)
    {
        if (ActiveDevice?.Id != update.DeviceId) return;

        dispatcher.EnqueueAsync(() =>
        {
            if (ActiveDevice?.Id != update.DeviceId) return;
            var existing = Conversations.FirstOrDefault(c => c.ThreadId == update.ThreadId);
            if (existing is null)
            {
                foreach (var remap in update.MessageIdRemaps)
                {
                    if (remap.TempThreadId is not long tempThreadId)
                        continue;
                    existing = Conversations.FirstOrDefault(c => c.ThreadId == tempThreadId);
                    if (existing is null)
                        continue;
                    existing.ThreadId = update.ThreadId;
                    existing.ConversationKey = update.Conversation.ConversationKey;
                    break;
                }
            }

            if (existing is not null)
            {
                existing.UpdateFrom(update.Conversation);
                MergeMessageIdsIntoIndex(existing, update.Messages);
                var currentIndex = Conversations.IndexOf(existing);
                var targetIndex = Conversations.Where(c => !ReferenceEquals(c, existing))
                    .TakeWhile(c => c.LastMessageTimestamp > existing.LastMessageTimestamp).Count();
                if (currentIndex != targetIndex)
                    Conversations.Move(currentIndex, targetIndex);
            }
            else
            {
                Conversations.Insert(
                    FindConversationInsertionIndex(Conversations, update.Conversation.LastMessageTimestamp),
                    update.Conversation);
                existing = update.Conversation;
            }

            var selected = SelectedConversation;
            if (selected is null)
                return;

            if (!ReferenceEquals(selected, existing) && selected.ThreadId != update.ThreadId)
                return;

            if (!ReferenceEquals(selected, existing))
            {
                selected.UpdateFrom(update.Conversation);
                MergeMessageIdsIntoIndex(selected, update.Messages);
            }

            foreach (var remap in update.MessageIdRemaps)
            {
                MessageIds.Remove(remap.TempMessageId);
                MessageIds.Add(remap.UniqueId);
                if (selected.MessageIndex is { } remapIndex)
                {
                    remapIndex.Remove(remap.TempMessageId);
                    remapIndex.AddNewest(remap.UniqueId);
                }
            }

            RemoveMessages(update.RemovedMessageIds);
            if (selected.MessageIndex is { } messageIndex)
                RemoveMessages(MessageIds.Where(id => id >= 0 && !messageIndex.Contains(id)).ToArray());
            UpdateMessages(update.Messages);
        });
    }

    private static void MergeMessageIdsIntoIndex(Conversation conversation, IEnumerable<Message> messages)
    {
        if (conversation.MessageIndex is not { } index)
            return;
        // Ids the index doesn't know yet are new arrivals; oldest first so the newest ends up at the front.
        foreach (var message in messages.OrderBy(m => m.Timestamp).ThenBy(m => m.UniqueId))
            index.AddNewest(message.UniqueId);
    }

    private static int FindConversationInsertionIndex(ObservableCollection<Conversation> conversations, long lastMessageTimestamp)
    {
        for (int i = 0; i < conversations.Count; i++)
        {
            if (conversations[i].LastMessageTimestamp <= lastMessageTimestamp)
                return i;
        }
        return conversations.Count;
    }

    private void AddMessages(IEnumerable<Message> messages)
    {
        foreach (var message in messages.OrderBy(m => m.Timestamp).ThenBy(m => m.UniqueId))
            if (MessageIds.Add(message.UniqueId))
                MessageGrouping.AddMessage(MessageGroups, message);
        RefreshMessagePresentation();
    }

    private void UpdateMessages(IEnumerable<Message> messages)
    {
        foreach (var message in messages.OrderBy(m => m.Timestamp).ThenBy(m => m.UniqueId))
        {
            if (MessageIds.Add(message.UniqueId))
                MessageGrouping.AddMessage(MessageGroups, message);
            else
                MessageGrouping.UpdateMessage(MessageGroups, message);
        }
        RefreshMessagePresentation();
    }

    private void RemoveMessages(IEnumerable<long> ids)
    {
        var removed = ids.ToHashSet();
        if (removed.Count == 0)
            return;

        MessageGrouping.RemoveMessages(MessageGroups, removed);
        MessageIds.ExceptWith(removed);
    }

    private void RefreshMessagePresentation()
    {
        MessageGrouping.RefreshHeaders(MessageGroups);
        RefreshSendIndicators();
    }

    private void RefreshSendIndicators()
    {
        var latestSuccess = MessageGroups.Where(g => !g.IsReceived).SelectMany(g => g.Messages)
            .Where(m => m.SendState is not (OutgoingSendState.Queued or OutgoingSendState.Sending))
            .OrderBy(m => m.Timestamp).LastOrDefault();
        foreach (var group in MessageGroups)
        {
            if (group.IsReceived)
                continue;

            foreach (var message in group.Messages)
            {
                var inFlight = message.SendState is OutgoingSendState.Queued or OutgoingSendState.Sending;
                var isLatestSuccess = ReferenceEquals(message, latestSuccess);
                message.ShowSendIndicator = inFlight || isLatestSuccess;
                message.ShowSendCheck = isLatestSuccess;
            }
        }
    }

}
