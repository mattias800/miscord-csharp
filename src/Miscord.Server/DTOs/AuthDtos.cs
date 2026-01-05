using System.ComponentModel.DataAnnotations;

namespace Miscord.Server.DTOs;

public record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, EmailAddress] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(
    Guid UserId,
    string Username,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

public record RefreshTokenRequest([Required] string RefreshToken);

public record UserProfileResponse(
    Guid Id,
    string Username,
    string Email,
    string? Avatar,
    string? Status,
    bool IsOnline,
    DateTime CreatedAt
);

public record UpdateProfileRequest(
    [StringLength(50, MinimumLength = 3)] string? Username,
    string? Avatar,
    string? Status
);
