using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace Snacka.Client.Services;

/// <summary>
/// Centralized state service for conversation data.
/// Uses DynamicData's SourceCache for reactive state management.
/// All conversation state flows through this service - ViewModels observe rather than own the state.
/// </summary>
public sealed class ConversationStateService : IConversationStateService, IDisposable
{
    private readonly IApiClient _apiClient;
    private readonly Guid _currentUserId;

    // The canonical source of truth for conversations
    private readonly SourceCache<ConversationSummaryResponse, Guid> _conversationsCache;

    // Bound collection for UI binding
    private readonly ReadOnlyObservableCollection<ConversationSummaryResponse> _conversations;

    // Track user ID to conversation ID mapping for 1:1 conversations
    private readonly Dictionary<Guid, Guid> _userToConversationMap = new();

    private readonly IDisposable _cleanUp;

    public ConversationStateService(IApiClient apiClient, ISignalRService signalR, Guid currentUserId)
    {
        _apiClient = apiClient;
        _currentUserId = currentUserId;

        // Initialize the source cache with conversation ID as the key
        _conversationsCache = new SourceCache<ConversationSummaryResponse, Guid>(c => c.Id);

        // Create a sorted, bound collection that automatically updates
        var sortedConversations = _conversationsCache
            .Connect()
            .SortBy(c => c.LastMessage?.CreatedAt ?? DateTime.MinValue, SortDirection.Descending)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _conversations)
            .Subscribe();

        _cleanUp = sortedConversations;

        // Subscribe to SignalR events for conversation updates
        signalR.ConversationMessageReceived += message =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnMessageReceived(message));
        signalR.ConversationMessageUpdated += message =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnMessageUpdated(message));
        signalR.ConversationMessageDeleted += e =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnMessageDeleted(e.ConversationId, e.MessageId));
        signalR.ConversationUpdated += conversation =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnConversationUpdated(conversation));
        signalR.AddedToConversation += _ =>
            Avalonia.Threading.Dispatcher.UIThread.Post(async () => await LoadConversationsAsync());
        signalR.RemovedFromConversation += conversationId =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnConversationRemoved(conversationId));
    }

    public IObservable<IChangeSet<ConversationSummaryResponse, Guid>> ConversationsChanges =>
        _conversationsCache.Connect();

    public ReadOnlyObservableCollection<ConversationSummaryResponse> Conversations => _conversations;

    public IObservable<int> TotalUnreadCount =>
        _conversationsCache
            .Connect()
            .QueryWhenChanged(items => items.Items.Sum(c => c.UnreadCount))
            .DistinctUntilChanged();

    public async Task LoadConversationsAsync()
    {
        var result = await _apiClient.GetConversationSummariesAsync();
        if (result.Success && result.Data is not null)
        {
            // Clear and reload
            _conversationsCache.Edit(cache =>
            {
                cache.Clear();
                cache.AddOrUpdate(result.Data);
            });

            // Rebuild user-to-conversation mapping for 1:1 conversations
            RebuildUserMapping();
        }
    }

    public async Task MarkConversationAsReadAsync(Guid conversationId)
    {
        // Optimistically update local state first for responsive UI
        var existing = _conversationsCache.Lookup(conversationId);
        if (existing.HasValue && existing.Value.UnreadCount > 0)
        {
            var updated = existing.Value with { UnreadCount = 0 };
            _conversationsCache.AddOrUpdate(updated);
        }

        // Then sync with server
        await _apiClient.MarkConversationReadByIdAsync(conversationId);
    }

    public void OnMessageReceived(ConversationMessageResponse message)
    {
        var existing = _conversationsCache.Lookup(message.ConversationId);

        if (existing.HasValue)
        {
            // Update existing conversation
            var conv = existing.Value;
            var newUnreadCount = message.SenderId != _currentUserId
                ? conv.UnreadCount + 1
                : conv.UnreadCount;

            var updated = conv with
            {
                LastMessage = message,
                UnreadCount = newUnreadCount
            };

            _conversationsCache.AddOrUpdate(updated);
        }
        else
        {
            // New conversation we don't know about - reload from server
            _ = LoadConversationsAsync();
        }
    }

    public void OnMessageUpdated(ConversationMessageResponse message)
    {
        var existing = _conversationsCache.Lookup(message.ConversationId);
        if (existing.HasValue)
        {
            var conv = existing.Value;
            // Only update if this is the last message
            if (conv.LastMessage?.Id == message.Id)
            {
                var updated = conv with { LastMessage = message };
                _conversationsCache.AddOrUpdate(updated);
            }
        }
    }

    public void OnMessageDeleted(Guid conversationId, Guid messageId)
    {
        var existing = _conversationsCache.Lookup(conversationId);
        if (existing.HasValue)
        {
            var conv = existing.Value;
            // If the deleted message was the last message, we need to reload to get the new last message
            if (conv.LastMessage?.Id == messageId)
            {
                _ = LoadConversationsAsync();
            }
        }
    }

    public int GetUnreadCountForUser(Guid userId)
    {
        if (_userToConversationMap.TryGetValue(userId, out var conversationId))
        {
            var conv = _conversationsCache.Lookup(conversationId);
            return conv.HasValue ? conv.Value.UnreadCount : 0;
        }
        return 0;
    }

    public ConversationSummaryResponse? GetConversation(Guid conversationId)
    {
        var result = _conversationsCache.Lookup(conversationId);
        return result.HasValue ? result.Value : null;
    }

    public void OnConversationAdded(ConversationSummaryResponse conversation)
    {
        _conversationsCache.AddOrUpdate(conversation);

        // Update user mapping if it's a 1:1 conversation
        if (!conversation.IsGroup)
        {
            // We need to figure out the other user - for now, reload to get full data
            _ = LoadConversationsAsync();
        }
    }

    public void OnConversationUpdated(ConversationResponse conversation)
    {
        var existing = _conversationsCache.Lookup(conversation.Id);
        if (existing.HasValue)
        {
            // Preserve existing summary data but update name/icon
            var updated = existing.Value with
            {
                DisplayName = GetDisplayName(conversation),
                IconFileName = conversation.IconFileName,
                IsGroup = conversation.IsGroup
            };
            _conversationsCache.AddOrUpdate(updated);
        }
    }

    public void OnConversationRemoved(Guid conversationId)
    {
        _conversationsCache.Remove(conversationId);
        RebuildUserMapping();
    }

    private void RebuildUserMapping()
    {
        _userToConversationMap.Clear();

        foreach (var conv in _conversationsCache.Items)
        {
            if (!conv.IsGroup && conv.OtherParticipantId.HasValue)
            {
                // For 1:1 conversations, map the other participant to this conversation
                _userToConversationMap[conv.OtherParticipantId.Value] = conv.Id;
            }
        }
    }

    private string GetDisplayName(ConversationResponse conversation)
    {
        if (!string.IsNullOrEmpty(conversation.Name))
            return conversation.Name;

        var otherParticipants = conversation.Participants
            .Where(p => p.UserId != _currentUserId)
            .ToList();

        if (otherParticipants.Count == 0)
            return "Empty Conversation";
        if (otherParticipants.Count == 1)
            return otherParticipants[0].EffectiveDisplayName;
        if (otherParticipants.Count == 2)
            return $"{otherParticipants[0].EffectiveDisplayName}, {otherParticipants[1].EffectiveDisplayName}";

        return $"{otherParticipants[0].EffectiveDisplayName} and {otherParticipants.Count - 1} others";
    }

    public void Dispose()
    {
        _cleanUp.Dispose();
        _conversationsCache.Dispose();
    }
}
