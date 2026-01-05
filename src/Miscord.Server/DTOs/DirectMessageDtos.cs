using System.ComponentModel.DataAnnotations;

namespace Miscord.Server.DTOs;

public record DirectMessageResponse(
    Guid Id,
    string Content,
    Guid SenderId,
    string SenderUsername,
    Guid RecipientId,
    string RecipientUsername,
    DateTime CreatedAt,
    bool IsRead
);

public record SendDirectMessageRequest([Required, StringLength(2000, MinimumLength = 1)] string Content);

public record ConversationSummary(
    Guid UserId,
    string Username,
    string? Avatar,
    bool IsOnline,
    DirectMessageResponse? LastMessage,
    int UnreadCount
);

public record DirectMessageUpdate(
    Guid MessageId,
    [Required, StringLength(2000, MinimumLength = 1)] string Content
);
