using Miscord.Server.DTOs;

namespace Miscord.Server.Services;

public interface IDirectMessageService
{
    Task<IEnumerable<DirectMessageResponse>> GetConversationAsync(
        Guid currentUserId, Guid otherUserId, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default);

    Task<DirectMessageResponse> SendMessageAsync(
        Guid senderId, Guid recipientId, string content,
        CancellationToken cancellationToken = default);

    Task<DirectMessageResponse> UpdateMessageAsync(
        Guid messageId, Guid userId, string content,
        CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<ConversationSummary>> GetConversationsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid currentUserId, Guid otherUserId, CancellationToken cancellationToken = default);
}
