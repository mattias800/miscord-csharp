using System.ComponentModel.DataAnnotations;
using Snacka.Shared.Models;

namespace Snacka.Server.DTOs;

// ============================================================================
// API Response DTOs
// ============================================================================

/// <summary>
/// Response for a gaming station.
/// </summary>
public record GamingStationResponse(
    Guid Id,
    Guid OwnerId,
    string OwnerUsername,
    string OwnerEffectiveDisplayName,
    string Name,
    string? Description,
    StationStatus Status,
    DateTime? LastSeenAt,
    DateTime CreatedAt,
    int ConnectedUserCount,
    bool IsOwner,
    StationPermission? MyPermission
);

/// <summary>
/// Response for a station access grant.
/// </summary>
public record StationAccessGrantResponse(
    Guid Id,
    Guid StationId,
    Guid UserId,
    string Username,
    string EffectiveDisplayName,
    string? Avatar,
    StationPermission Permission,
    Guid GrantedById,
    string GrantedByUsername,
    DateTime GrantedAt,
    DateTime? ExpiresAt
);

/// <summary>
/// Response for a station session.
/// </summary>
public record StationSessionResponse(
    Guid Id,
    Guid StationId,
    DateTime StartedAt,
    List<StationSessionUserResponse> ConnectedUsers
);

/// <summary>
/// Response for a user connected to a station session.
/// </summary>
public record StationSessionUserResponse(
    Guid UserId,
    string Username,
    string EffectiveDisplayName,
    string? Avatar,
    int? PlayerSlot,
    StationInputMode InputMode,
    DateTime ConnectedAt,
    DateTime? LastInputAt
);

// ============================================================================
// API Request DTOs
// ============================================================================

/// <summary>
/// Request to register a new gaming station.
/// </summary>
public record RegisterStationRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Name,
    [StringLength(256)] string? Description,
    [Required, StringLength(128)] string MachineId
);

/// <summary>
/// Request to update a gaming station.
/// </summary>
public record UpdateStationRequest(
    [StringLength(64, MinimumLength = 1)] string? Name,
    [StringLength(256)] string? Description
);

/// <summary>
/// Request to grant access to a station.
/// </summary>
public record GrantStationAccessRequest(
    [Required] Guid UserId,
    StationPermission Permission = StationPermission.Controller,
    DateTime? ExpiresAt = null
);

/// <summary>
/// Request to update an access grant.
/// </summary>
public record UpdateStationAccessRequest(
    StationPermission? Permission,
    DateTime? ExpiresAt
);

/// <summary>
/// Request to connect to a station.
/// </summary>
public record ConnectToStationRequest(
    StationInputMode PreferredInputMode = StationInputMode.Controller
);

/// <summary>
/// Request to assign a player slot.
/// </summary>
public record AssignPlayerSlotRequest(
    [Required] Guid UserId,
    [Range(1, 4)] int? PlayerSlot
);

// ============================================================================
// SignalR Event DTOs
// ============================================================================

/// <summary>
/// Event when a station comes online.
/// </summary>
public record StationOnlineEvent(
    Guid StationId,
    string StationName,
    Guid OwnerId
);

/// <summary>
/// Event when a station goes offline.
/// </summary>
public record StationOfflineEvent(
    Guid StationId
);

/// <summary>
/// Event when a station's status changes.
/// </summary>
public record StationStatusChangedEvent(
    Guid StationId,
    StationStatus Status,
    int ConnectedUserCount
);

/// <summary>
/// Event when a user connects to a station.
/// </summary>
public record UserConnectedToStationEvent(
    Guid StationId,
    Guid UserId,
    string Username,
    string EffectiveDisplayName,
    int? PlayerSlot,
    StationInputMode InputMode
);

/// <summary>
/// Event when a user disconnects from a station.
/// </summary>
public record UserDisconnectedFromStationEvent(
    Guid StationId,
    Guid UserId
);

/// <summary>
/// Event when a player slot is assigned.
/// </summary>
public record PlayerSlotAssignedEvent(
    Guid StationId,
    Guid UserId,
    int? PlayerSlot
);

/// <summary>
/// Event when access is granted to a station.
/// </summary>
public record StationAccessGrantedEvent(
    Guid StationId,
    string StationName,
    Guid UserId,
    StationPermission Permission,
    Guid GrantedById,
    string GrantedByUsername
);

/// <summary>
/// Event when access is revoked from a station.
/// </summary>
public record StationAccessRevokedEvent(
    Guid StationId,
    Guid UserId,
    Guid RevokedById
);

/// <summary>
/// Event when a user wants to connect to a station (sent to station).
/// </summary>
public record UserConnectingToStationEvent(
    Guid UserId,
    string Username,
    string EffectiveDisplayName,
    StationInputMode RequestedInputMode
);

// ============================================================================
// WebRTC Signaling DTOs (Station-specific)
// ============================================================================

/// <summary>
/// WebRTC offer from the station to a connecting user.
/// </summary>
public record StationWebRtcOffer(
    Guid StationId,
    Guid UserId,
    string Sdp
);

/// <summary>
/// WebRTC answer from a user to the station.
/// </summary>
public record StationWebRtcAnswer(
    Guid StationId,
    string Sdp
);

/// <summary>
/// ICE candidate for station WebRTC connection.
/// </summary>
public record StationIceCandidate(
    Guid StationId,
    Guid? UserId,
    string Candidate,
    string? SdpMid,
    int? SdpMLineIndex
);

// ============================================================================
// Input DTOs (sent from connected users to station)
// ============================================================================

/// <summary>
/// Keyboard input event.
/// </summary>
public record StationKeyboardInput(
    Guid StationId,
    string Key,
    bool IsDown,
    bool Ctrl,
    bool Alt,
    bool Shift,
    bool Meta
);

/// <summary>
/// Mouse input event.
/// </summary>
public record StationMouseInput(
    Guid StationId,
    StationMouseInputType Type,
    double X,
    double Y,
    int? Button,
    double? DeltaX,
    double? DeltaY
);

/// <summary>
/// Type of mouse input.
/// </summary>
public enum StationMouseInputType
{
    Move = 0,
    Down = 1,
    Up = 2,
    Wheel = 3
}

/// <summary>
/// Controller input (reuses existing ControllerState pattern).
/// </summary>
public record StationControllerInput(
    Guid StationId,
    int PlayerSlot,
    ushort Buttons,
    byte LeftTrigger,
    byte RightTrigger,
    short LeftStickX,
    short LeftStickY,
    short RightStickX,
    short RightStickY
);
