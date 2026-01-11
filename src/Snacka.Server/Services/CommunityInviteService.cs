using Microsoft.EntityFrameworkCore;
using Snacka.Server.Data;
using Snacka.Server.DTOs;
using Snacka.Shared.Models;

namespace Snacka.Server.Services;

public interface ICommunityInviteService
{
    /// <summary>
    /// Create an invite for a user to join a community.
    /// </summary>
    Task<CommunityInviteResponse> CreateInviteAsync(Guid communityId, Guid invitedUserId, Guid invitedById, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all pending invites for a user.
    /// </summary>
    Task<IEnumerable<CommunityInviteResponse>> GetPendingInvitesForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all invites sent from a community (for admins).
    /// </summary>
    Task<IEnumerable<CommunityInviteResponse>> GetInvitesForCommunityAsync(Guid communityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accept an invite and join the community.
    /// </summary>
    Task<CommunityInviteResponse> AcceptInviteAsync(Guid inviteId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decline an invite.
    /// </summary>
    Task<CommunityInviteResponse> DeclineInviteAsync(Guid inviteId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a pending invite (by the inviter or community admin).
    /// </summary>
    Task CancelInviteAsync(Guid inviteId, Guid requestingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for users to invite (excludes current members).
    /// </summary>
    Task<IEnumerable<UserSearchResult>> SearchUsersToInviteAsync(Guid communityId, string query, CancellationToken cancellationToken = default);
}

public sealed class CommunityInviteService : ICommunityInviteService
{
    private readonly SnackaDbContext _db;
    private readonly ICommunityMemberService _memberService;

    public CommunityInviteService(SnackaDbContext db, ICommunityMemberService memberService)
    {
        _db = db;
        _memberService = memberService;
    }

    public async Task<CommunityInviteResponse> CreateInviteAsync(Guid communityId, Guid invitedUserId, Guid invitedById, CancellationToken cancellationToken = default)
    {
        // Verify community exists
        var community = await _db.Communities.FindAsync([communityId], cancellationToken)
            ?? throw new InvalidOperationException("Community not found.");

        // Verify inviter is owner or admin
        var inviterMembership = await _db.UserCommunities
            .FirstOrDefaultAsync(uc => uc.UserId == invitedById && uc.CommunityId == communityId, cancellationToken)
            ?? throw new UnauthorizedAccessException("You are not a member of this community.");

        if (inviterMembership.Role != UserRole.Owner && inviterMembership.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("Only owners and admins can invite users.");

        // Verify invited user exists
        var invitedUser = await _db.Users.FindAsync([invitedUserId], cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        // Check if user is already a member
        var isMember = await _memberService.IsMemberAsync(communityId, invitedUserId, cancellationToken);
        if (isMember)
            throw new InvalidOperationException("User is already a member of this community.");

        // Check for existing pending invite
        var existingInvite = await _db.CommunityInvites
            .FirstOrDefaultAsync(ci => ci.CommunityId == communityId
                && ci.InvitedUserId == invitedUserId
                && ci.Status == CommunityInviteStatus.Pending, cancellationToken);

        if (existingInvite != null)
            throw new InvalidOperationException("An invite is already pending for this user.");

        // Create the invite
        var invite = new CommunityInvite
        {
            CommunityId = communityId,
            InvitedUserId = invitedUserId,
            InvitedById = invitedById
        };

        _db.CommunityInvites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        // Load relationships for response
        await _db.Entry(invite).Reference(i => i.Community).LoadAsync(cancellationToken);
        await _db.Entry(invite).Reference(i => i.InvitedUser).LoadAsync(cancellationToken);
        await _db.Entry(invite).Reference(i => i.InvitedBy).LoadAsync(cancellationToken);

        return CreateInviteResponse(invite);
    }

    public async Task<IEnumerable<CommunityInviteResponse>> GetPendingInvitesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var invites = await _db.CommunityInvites
            .Include(ci => ci.Community)
            .Include(ci => ci.InvitedUser)
            .Include(ci => ci.InvitedBy)
            .Where(ci => ci.InvitedUserId == userId && ci.Status == CommunityInviteStatus.Pending)
            .OrderByDescending(ci => ci.CreatedAt)
            .ToListAsync(cancellationToken);

        return invites.Select(CreateInviteResponse);
    }

    public async Task<IEnumerable<CommunityInviteResponse>> GetInvitesForCommunityAsync(Guid communityId, CancellationToken cancellationToken = default)
    {
        var invites = await _db.CommunityInvites
            .Include(ci => ci.Community)
            .Include(ci => ci.InvitedUser)
            .Include(ci => ci.InvitedBy)
            .Where(ci => ci.CommunityId == communityId)
            .OrderByDescending(ci => ci.CreatedAt)
            .ToListAsync(cancellationToken);

        return invites.Select(CreateInviteResponse);
    }

    public async Task<CommunityInviteResponse> AcceptInviteAsync(Guid inviteId, Guid userId, CancellationToken cancellationToken = default)
    {
        var invite = await _db.CommunityInvites
            .Include(ci => ci.Community)
            .Include(ci => ci.InvitedUser)
            .Include(ci => ci.InvitedBy)
            .FirstOrDefaultAsync(ci => ci.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("Invite not found.");

        if (invite.InvitedUserId != userId)
            throw new UnauthorizedAccessException("This invite is not for you.");

        if (invite.Status != CommunityInviteStatus.Pending)
            throw new InvalidOperationException("This invite has already been responded to.");

        // Update invite status
        invite.Status = CommunityInviteStatus.Accepted;
        invite.RespondedAt = DateTime.UtcNow;

        // Add user to community
        await _memberService.JoinCommunityAsync(invite.CommunityId, userId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return CreateInviteResponse(invite);
    }

    public async Task<CommunityInviteResponse> DeclineInviteAsync(Guid inviteId, Guid userId, CancellationToken cancellationToken = default)
    {
        var invite = await _db.CommunityInvites
            .Include(ci => ci.Community)
            .Include(ci => ci.InvitedUser)
            .Include(ci => ci.InvitedBy)
            .FirstOrDefaultAsync(ci => ci.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("Invite not found.");

        if (invite.InvitedUserId != userId)
            throw new UnauthorizedAccessException("This invite is not for you.");

        if (invite.Status != CommunityInviteStatus.Pending)
            throw new InvalidOperationException("This invite has already been responded to.");

        invite.Status = CommunityInviteStatus.Declined;
        invite.RespondedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return CreateInviteResponse(invite);
    }

    public async Task CancelInviteAsync(Guid inviteId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var invite = await _db.CommunityInvites
            .FirstOrDefaultAsync(ci => ci.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("Invite not found.");

        if (invite.Status != CommunityInviteStatus.Pending)
            throw new InvalidOperationException("Only pending invites can be cancelled.");

        // Check if requester is the inviter or a community admin/owner
        if (invite.InvitedById != requestingUserId)
        {
            var membership = await _db.UserCommunities
                .FirstOrDefaultAsync(uc => uc.UserId == requestingUserId && uc.CommunityId == invite.CommunityId, cancellationToken);

            if (membership == null || (membership.Role != UserRole.Owner && membership.Role != UserRole.Admin))
                throw new UnauthorizedAccessException("Only the inviter or community admins can cancel invites.");
        }

        _db.CommunityInvites.Remove(invite);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserSearchResult>> SearchUsersToInviteAsync(Guid communityId, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        // Get current member IDs
        var memberIds = await _db.UserCommunities
            .Where(uc => uc.CommunityId == communityId)
            .Select(uc => uc.UserId)
            .ToListAsync(cancellationToken);

        // Get users with pending invites
        var pendingInviteUserIds = await _db.CommunityInvites
            .Where(ci => ci.CommunityId == communityId && ci.Status == CommunityInviteStatus.Pending)
            .Select(ci => ci.InvitedUserId)
            .ToListAsync(cancellationToken);

        // Search users by username, excluding members and users with pending invites
        var users = await _db.Users
            .Where(u => !memberIds.Contains(u.Id)
                && !pendingInviteUserIds.Contains(u.Id)
                && (u.Username.ToLower().Contains(query.ToLower())
                    || (u.DisplayName != null && u.DisplayName.ToLower().Contains(query.ToLower()))))
            .Take(20)
            .ToListAsync(cancellationToken);

        return users.Select(u => new UserSearchResult(
            u.Id,
            u.Username,
            u.EffectiveDisplayName,
            u.AvatarFileName,
            u.IsOnline
        ));
    }

    private static CommunityInviteResponse CreateInviteResponse(CommunityInvite invite)
    {
        var community = invite.Community;
        var invitedUser = invite.InvitedUser;
        var invitedBy = invite.InvitedBy;

        return new CommunityInviteResponse(
            invite.Id,
            invite.CommunityId,
            community?.Name ?? "Unknown",
            community?.Icon,
            invite.InvitedUserId,
            invitedUser?.Username ?? "Unknown",
            invitedUser?.EffectiveDisplayName ?? "Unknown",
            invite.InvitedById,
            invitedBy?.Username ?? "Unknown",
            invitedBy?.EffectiveDisplayName ?? "Unknown",
            invite.Status,
            invite.CreatedAt,
            invite.RespondedAt
        );
    }
}
