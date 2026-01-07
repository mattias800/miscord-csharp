using System.ComponentModel.DataAnnotations;

namespace Miscord.Server.DTOs;

public record CreateInviteRequest(
    int MaxUses = 0,
    DateTime? ExpiresAt = null
);

public record ServerInviteResponse(
    Guid Id,
    string Code,
    int MaxUses,
    int CurrentUses,
    DateTime? ExpiresAt,
    bool IsRevoked,
    string? CreatedByUsername,
    DateTime CreatedAt
);

public record AdminUserResponse(
    Guid Id,
    string Username,
    string Email,
    bool IsServerAdmin,
    bool IsOnline,
    DateTime CreatedAt,
    string? InvitedByUsername
);

public record SetAdminStatusRequest(
    [Required] bool IsAdmin
);
