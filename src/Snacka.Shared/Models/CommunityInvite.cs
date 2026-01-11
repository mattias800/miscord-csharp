namespace Snacka.Shared.Models;

/// <summary>
/// Represents an invitation to join a community sent to a specific user.
/// </summary>
public class CommunityInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The community the user is being invited to.
    /// </summary>
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }

    /// <summary>
    /// The user being invited.
    /// </summary>
    public Guid InvitedUserId { get; set; }
    public User? InvitedUser { get; set; }

    /// <summary>
    /// The user who sent the invite (must be owner or admin of the community).
    /// </summary>
    public Guid InvitedById { get; set; }
    public User? InvitedBy { get; set; }

    /// <summary>
    /// Current status of the invite.
    /// </summary>
    public CommunityInviteStatus Status { get; set; } = CommunityInviteStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the invite was responded to (accepted or declined).
    /// </summary>
    public DateTime? RespondedAt { get; set; }
}

public enum CommunityInviteStatus
{
    Pending,
    Accepted,
    Declined
}
