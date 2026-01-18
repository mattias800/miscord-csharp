using System.ComponentModel.DataAnnotations;

namespace Snacka.Shared.Models;

/// <summary>
/// Represents a registered gaming PC that can be accessed remotely.
/// </summary>
public class GamingStation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The user who owns this gaming station.
    /// </summary>
    public required Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>
    /// Display name for the station (e.g., "My Gaming Rig").
    /// </summary>
    [MaxLength(64)]
    public required string Name { get; set; }

    /// <summary>
    /// Optional description of the station.
    /// </summary>
    [MaxLength(256)]
    public string? Description { get; set; }

    /// <summary>
    /// Unique identifier for the machine (hardware ID or generated on first registration).
    /// Used to verify the station is connecting from the registered device.
    /// </summary>
    [MaxLength(128)]
    public required string MachineId { get; set; }

    /// <summary>
    /// Current status of the station.
    /// </summary>
    public StationStatus Status { get; set; } = StationStatus.Offline;

    /// <summary>
    /// The SignalR connection ID of the station when online.
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Last time the station was seen online.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// When the station was registered.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time the station settings were updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Users who have been granted access to this station.
    /// </summary>
    public ICollection<StationAccessGrant> AccessGrants { get; set; } = new List<StationAccessGrant>();

    /// <summary>
    /// Active sessions on this station.
    /// </summary>
    public ICollection<StationSession> Sessions { get; set; } = new List<StationSession>();
}

/// <summary>
/// The current status of a gaming station.
/// </summary>
public enum StationStatus
{
    /// <summary>
    /// Station is not connected.
    /// </summary>
    Offline = 0,

    /// <summary>
    /// Station is connected and ready for connections.
    /// </summary>
    Online = 1,

    /// <summary>
    /// Station is connected and has active users.
    /// </summary>
    InUse = 2,

    /// <summary>
    /// Station is in maintenance mode (owner only).
    /// </summary>
    Maintenance = 3
}
