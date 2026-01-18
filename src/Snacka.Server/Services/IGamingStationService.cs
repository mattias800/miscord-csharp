using Snacka.Server.DTOs;
using Snacka.Shared.Models;

namespace Snacka.Server.Services;

public interface IGamingStationService
{
    // ========================================================================
    // Station Management
    // ========================================================================

    /// <summary>
    /// Get all stations the user owns or has access to.
    /// </summary>
    Task<IEnumerable<GamingStationResponse>> GetStationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific station by ID.
    /// </summary>
    Task<GamingStationResponse?> GetStationAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a new gaming station.
    /// </summary>
    Task<GamingStationResponse> RegisterStationAsync(
        Guid userId,
        RegisterStationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a station's settings.
    /// </summary>
    Task<GamingStationResponse> UpdateStationAsync(
        Guid stationId,
        Guid userId,
        UpdateStationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregister (delete) a station.
    /// </summary>
    Task DeleteStationAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // Station Status
    // ========================================================================

    /// <summary>
    /// Mark a station as online (called when station connects).
    /// </summary>
    Task<GamingStation?> SetStationOnlineAsync(
        Guid stationId,
        Guid userId,
        string machineId,
        string connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a station as offline (called when station disconnects).
    /// </summary>
    Task SetStationOfflineAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the station associated with a connection ID.
    /// </summary>
    Task<GamingStation?> GetStationByConnectionIdAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // Access Management
    // ========================================================================

    /// <summary>
    /// Get all access grants for a station.
    /// </summary>
    Task<IEnumerable<StationAccessGrantResponse>> GetAccessGrantsAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grant access to a user.
    /// </summary>
    Task<StationAccessGrantResponse> GrantAccessAsync(
        Guid stationId,
        Guid granterId,
        GrantStationAccessRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing access grant.
    /// </summary>
    Task<StationAccessGrantResponse> UpdateAccessAsync(
        Guid stationId,
        Guid grantId,
        Guid userId,
        UpdateStationAccessRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke access from a user.
    /// </summary>
    Task RevokeAccessAsync(
        Guid stationId,
        Guid targetUserId,
        Guid revokerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a user has access to a station and what permission level.
    /// </summary>
    Task<StationPermission?> GetUserPermissionAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // Session Management
    // ========================================================================

    /// <summary>
    /// Get the current active session for a station.
    /// </summary>
    Task<StationSessionResponse?> GetActiveSessionAsync(
        Guid stationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Connect a user to a station (creates or joins session).
    /// </summary>
    Task<StationSessionUserResponse> ConnectUserAsync(
        Guid stationId,
        Guid userId,
        string connectionId,
        StationInputMode inputMode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect a user from a station.
    /// </summary>
    Task DisconnectUserAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect a user by their connection ID (for disconnection handling).
    /// </summary>
    Task DisconnectUserByConnectionIdAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign a player slot to a connected user.
    /// </summary>
    Task<StationSessionUserResponse?> AssignPlayerSlotAsync(
        Guid stationId,
        Guid userId,
        int? playerSlot,
        Guid assignerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the last input time for a user.
    /// </summary>
    Task UpdateLastInputAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
