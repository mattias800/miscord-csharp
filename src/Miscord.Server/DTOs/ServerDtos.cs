using System.ComponentModel.DataAnnotations;
using Miscord.Shared.Models;

namespace Miscord.Server.DTOs;

public record CommunityResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    Guid OwnerId,
    string OwnerUsername,
    DateTime CreatedAt,
    int MemberCount
);

public record CreateCommunityRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [StringLength(1000)] string? Description
);

public record UpdateCommunityRequest(
    [StringLength(100, MinimumLength = 1)] string? Name,
    [StringLength(1000)] string? Description,
    string? Icon
);

public record ChannelResponse(
    Guid Id,
    string Name,
    string? Topic,
    Guid CommunityId,
    ChannelType Type,
    int Position,
    DateTime CreatedAt
);

public record CreateChannelRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [StringLength(1000)] string? Topic,
    ChannelType Type = ChannelType.Text
);

public record UpdateChannelRequest(
    [StringLength(100, MinimumLength = 1)] string? Name,
    [StringLength(1000)] string? Topic,
    int? Position
);

public record MessageResponse(
    Guid Id,
    string Content,
    Guid AuthorId,
    string AuthorUsername,
    string? AuthorAvatar,
    Guid ChannelId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsEdited
);

public record SendMessageRequest([Required, StringLength(2000, MinimumLength = 1)] string Content);

public record UpdateMessageRequest([Required, StringLength(2000, MinimumLength = 1)] string Content);

public record CommunityMemberResponse(
    Guid UserId,
    string Username,
    string? Avatar,
    bool IsOnline,
    UserRole Role,
    DateTime JoinedAt
);

public record UpdateMemberRoleRequest([Required] UserRole Role);

public record TransferOwnershipRequest([Required] Guid NewOwnerId);

// SignalR Event DTOs - used for type-safe event broadcasting
public record ChannelDeletedEvent(Guid ChannelId);

public record MessageDeletedEvent(Guid ChannelId, Guid MessageId);

public record UserOfflineEvent(Guid UserId);
