using System.Collections.ObjectModel;
using DynamicData;

namespace Snacka.Client.Services;

/// <summary>
/// Centralized state service for conversation data.
/// Provides a single source of truth for conversation state that multiple ViewModels can observe.
/// This follows the canonical state pattern - all conversation-related state flows through this service.
/// </summary>
public interface IConversationStateService
{
    /// <summary>
    /// Observable stream of conversation changes. ViewModels subscribe to this to stay in sync.
    /// </summary>
    IObservable<IChangeSet<ConversationSummaryResponse, Guid>> ConversationsChanges { get; }

    /// <summary>
    /// Read-only collection of current conversations for binding.
    /// </summary>
    ReadOnlyObservableCollection<ConversationSummaryResponse> Conversations { get; }

    /// <summary>
    /// Total unread count across all conversations.
    /// </summary>
    IObservable<int> TotalUnreadCount { get; }

    /// <summary>
    /// Loads conversations from the server. Call this on startup and reconnection.
    /// </summary>
    Task LoadConversationsAsync();

    /// <summary>
    /// Marks a conversation as read, updating both server and local state.
    /// All subscribers will be notified of the change.
    /// </summary>
    Task MarkConversationAsReadAsync(Guid conversationId);

    /// <summary>
    /// Called when a new message is received via SignalR.
    /// Updates the conversation's last message and unread count.
    /// </summary>
    void OnMessageReceived(ConversationMessageResponse message);

    /// <summary>
    /// Called when a message is updated via SignalR.
    /// </summary>
    void OnMessageUpdated(ConversationMessageResponse message);

    /// <summary>
    /// Called when a message is deleted via SignalR.
    /// </summary>
    void OnMessageDeleted(Guid conversationId, Guid messageId);

    /// <summary>
    /// Gets the unread count for a specific user's 1:1 conversation (if one exists).
    /// Used by members list to show unread badges.
    /// </summary>
    int GetUnreadCountForUser(Guid userId);

    /// <summary>
    /// Gets a conversation by ID.
    /// </summary>
    ConversationSummaryResponse? GetConversation(Guid conversationId);

    /// <summary>
    /// Called when a new conversation is created or the user is added to one.
    /// </summary>
    void OnConversationAdded(ConversationSummaryResponse conversation);

    /// <summary>
    /// Called when a conversation is updated (name, icon, etc.).
    /// </summary>
    void OnConversationUpdated(ConversationResponse conversation);

    /// <summary>
    /// Called when the user is removed from a conversation.
    /// </summary>
    void OnConversationRemoved(Guid conversationId);
}
