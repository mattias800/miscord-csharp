namespace Snacka.Shared.Models;

/// <summary>
/// Represents an active session on a gaming station.
/// </summary>
public class StationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The station this session is on.
    /// </summary>
    public required Guid StationId { get; set; }
    public GamingStation? Station { get; set; }

    /// <summary>
    /// When the session started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session ended (null if still active).
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// Whether the session is currently active.
    /// </summary>
    public bool IsActive => EndedAt == null;

    /// <summary>
    /// Users connected to this session.
    /// </summary>
    public ICollection<StationSessionUser> ConnectedUsers { get; set; } = new List<StationSessionUser>();
}

/// <summary>
/// Represents a user connected to a station session.
/// </summary>
public class StationSessionUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The session this user is connected to.
    /// </summary>
    public required Guid SessionId { get; set; }
    public StationSession? Session { get; set; }

    /// <summary>
    /// The connected user.
    /// </summary>
    public required Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// The SignalR connection ID for this user's connection.
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// The assigned player slot (1-4, or null if view-only).
    /// </summary>
    public int? PlayerSlot { get; set; }

    /// <summary>
    /// The input mode for this user.
    /// </summary>
    public StationInputMode InputMode { get; set; } = StationInputMode.ViewOnly;

    /// <summary>
    /// When the user connected.
    /// </summary>
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user disconnected (null if still connected).
    /// </summary>
    public DateTime? DisconnectedAt { get; set; }

    /// <summary>
    /// Last time this user sent input.
    /// </summary>
    public DateTime? LastInputAt { get; set; }
}

/// <summary>
/// Input mode for a connected user.
/// </summary>
public enum StationInputMode
{
    /// <summary>
    /// View only, no input allowed.
    /// </summary>
    ViewOnly = 0,

    /// <summary>
    /// Controller input only.
    /// </summary>
    Controller = 1,

    /// <summary>
    /// Full input (controller, keyboard, mouse).
    /// </summary>
    FullInput = 2
}
