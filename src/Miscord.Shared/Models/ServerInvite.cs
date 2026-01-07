namespace Miscord.Shared.Models;

/// <summary>
/// Represents an invite code for registering on the server.
/// Server admins can create invites to allow new users to register.
/// </summary>
public class ServerInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The invite code string (e.g., "abc123xy")
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// The user who created this invite. Null for bootstrap invites.
    /// </summary>
    public Guid? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Maximum number of times this invite can be used. 0 = unlimited.
    /// </summary>
    public int MaxUses { get; set; } = 0;

    /// <summary>
    /// Number of times this invite has been used.
    /// </summary>
    public int CurrentUses { get; set; } = 0;

    /// <summary>
    /// When this invite expires. Null = never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether this invite has been revoked by an admin.
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
