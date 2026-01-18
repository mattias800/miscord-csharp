namespace Snacka.Shared.Models;

/// <summary>
/// Represents access granted to a user for a gaming station.
/// </summary>
public class StationAccessGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The station this grant is for.
    /// </summary>
    public required Guid StationId { get; set; }
    public GamingStation? Station { get; set; }

    /// <summary>
    /// The user who has been granted access.
    /// </summary>
    public required Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// The permission level granted.
    /// </summary>
    public StationPermission Permission { get; set; } = StationPermission.Controller;

    /// <summary>
    /// User who granted this access.
    /// </summary>
    public required Guid GrantedById { get; set; }
    public User? GrantedBy { get; set; }

    /// <summary>
    /// When the access was granted.
    /// </summary>
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional expiration time for time-limited access.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Permission levels for station access.
/// </summary>
public enum StationPermission
{
    /// <summary>
    /// Can only view the stream, no input allowed.
    /// </summary>
    ViewOnly = 0,

    /// <summary>
    /// Can view and send controller input.
    /// </summary>
    Controller = 1,

    /// <summary>
    /// Can view and send all input (controller, keyboard, mouse).
    /// </summary>
    FullControl = 2,

    /// <summary>
    /// Full control plus can manage other users' access.
    /// </summary>
    Admin = 3
}
