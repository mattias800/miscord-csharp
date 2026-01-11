using Snacka.Shared.Models;

namespace Snacka.Server.Services;

public interface IServerInviteService
{
    /// <summary>
    /// Creates a new server invite code.
    /// </summary>
    /// <param name="creatorId">The user creating the invite (null for bootstrap invites)</param>
    /// <param name="maxUses">Maximum uses (0 = unlimited)</param>
    /// <param name="expiresAt">Expiration date (null = never expires)</param>
    Task<ServerInvite> CreateInviteAsync(Guid? creatorId, int maxUses = 0, DateTime? expiresAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an invite code and returns the invite if valid.
    /// </summary>
    Task<ServerInvite?> ValidateInviteCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an invite as used (increments CurrentUses).
    /// </summary>
    Task UseInviteAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all invites for the admin panel.
    /// </summary>
    Task<IEnumerable<ServerInvite>> GetAllInvitesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an invite by ID.
    /// </summary>
    Task RevokeInviteAsync(Guid inviteId, Guid requestingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the server has any registered users.
    /// </summary>
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a bootstrap invite code for first-time server setup.
    /// Returns null if users already exist.
    /// </summary>
    Task<string?> GetOrCreateBootstrapInviteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the inviter's user ID for a given invite code.
    /// </summary>
    Task<Guid?> GetInviterIdAsync(string code, CancellationToken cancellationToken = default);
}
