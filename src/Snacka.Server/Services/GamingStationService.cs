using Microsoft.EntityFrameworkCore;
using Snacka.Server.Data;
using Snacka.Server.DTOs;
using Snacka.Shared.Models;

namespace Snacka.Server.Services;

public sealed class GamingStationService : IGamingStationService
{
    private readonly SnackaDbContext _db;

    public GamingStationService(SnackaDbContext db) => _db = db;

    // ========================================================================
    // Station Management
    // ========================================================================

    public async Task<IEnumerable<GamingStationResponse>> GetStationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Get stations the user owns
        var ownedStations = await _db.GamingStations
            .Include(s => s.Owner)
            .Include(s => s.Sessions.Where(sess => sess.EndedAt == null))
                .ThenInclude(sess => sess.ConnectedUsers.Where(u => u.DisconnectedAt == null))
            .Where(s => s.OwnerId == userId)
            .ToListAsync(cancellationToken);

        // Get stations the user has access to (not owned)
        var accessibleStationIds = await _db.StationAccessGrants
            .Where(g => g.UserId == userId && (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow))
            .Select(g => g.StationId)
            .ToListAsync(cancellationToken);

        var sharedStations = await _db.GamingStations
            .Include(s => s.Owner)
            .Include(s => s.Sessions.Where(sess => sess.EndedAt == null))
                .ThenInclude(sess => sess.ConnectedUsers.Where(u => u.DisconnectedAt == null))
            .Include(s => s.AccessGrants.Where(g => g.UserId == userId))
            .Where(s => accessibleStationIds.Contains(s.Id) && s.OwnerId != userId)
            .ToListAsync(cancellationToken);

        var result = new List<GamingStationResponse>();

        foreach (var station in ownedStations)
        {
            result.Add(ToStationResponse(station, userId, isOwner: true, permission: null));
        }

        foreach (var station in sharedStations)
        {
            var grant = station.AccessGrants.FirstOrDefault(g => g.UserId == userId);
            result.Add(ToStationResponse(station, userId, isOwner: false, permission: grant?.Permission));
        }

        return result.OrderBy(s => !s.IsOwner).ThenBy(s => s.Name);
    }

    public async Task<GamingStationResponse?> GetStationAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .Include(s => s.Owner)
            .Include(s => s.Sessions.Where(sess => sess.EndedAt == null))
                .ThenInclude(sess => sess.ConnectedUsers.Where(u => u.DisconnectedAt == null))
            .Include(s => s.AccessGrants.Where(g => g.UserId == userId))
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken);

        if (station == null)
            return null;

        // Check if user has access
        var isOwner = station.OwnerId == userId;
        var grant = station.AccessGrants.FirstOrDefault(g => g.UserId == userId);

        if (!isOwner && grant == null)
            return null;

        return ToStationResponse(station, userId, isOwner, grant?.Permission);
    }

    public async Task<GamingStationResponse> RegisterStationAsync(
        Guid userId,
        RegisterStationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Check if a station with this machine ID already exists for this user
        var existing = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.OwnerId == userId && s.MachineId == request.MachineId, cancellationToken);

        if (existing != null)
            throw new InvalidOperationException("A station with this machine ID is already registered.");

        var station = new GamingStation
        {
            OwnerId = userId,
            Name = request.Name,
            Description = request.Description,
            MachineId = request.MachineId,
            Status = StationStatus.Offline
        };

        _db.GamingStations.Add(station);
        await _db.SaveChangesAsync(cancellationToken);

        // Reload with owner
        await _db.Entry(station).Reference(s => s.Owner).LoadAsync(cancellationToken);

        return ToStationResponse(station, userId, isOwner: true, permission: null);
    }

    public async Task<GamingStationResponse> UpdateStationAsync(
        Guid stationId,
        Guid userId,
        UpdateStationRequest request,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        // Only owner can update
        if (station.OwnerId != userId)
            throw new UnauthorizedAccessException("Only the station owner can update settings.");

        if (request.Name != null)
            station.Name = request.Name;
        if (request.Description != null)
            station.Description = request.Description;

        station.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return ToStationResponse(station, userId, isOwner: true, permission: null);
    }

    public async Task DeleteStationAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        if (station.OwnerId != userId)
            throw new UnauthorizedAccessException("Only the station owner can delete the station.");

        _db.GamingStations.Remove(station);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ========================================================================
    // Station Status
    // ========================================================================

    public async Task<GamingStation?> SetStationOnlineAsync(
        Guid stationId,
        Guid userId,
        string machineId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken);

        if (station == null)
            return null;

        // Verify ownership and machine ID
        if (station.OwnerId != userId)
            return null;
        if (station.MachineId != machineId)
            return null;

        station.Status = StationStatus.Online;
        station.ConnectionId = connectionId;
        station.LastSeenAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return station;
    }

    public async Task SetStationOfflineAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.ConnectionId == connectionId, cancellationToken);

        if (station == null)
            return;

        station.Status = StationStatus.Offline;
        station.ConnectionId = null;
        station.LastSeenAt = DateTime.UtcNow;

        // End any active sessions
        var activeSessions = await _db.StationSessions
            .Where(s => s.StationId == station.Id && s.EndedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.EndedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<GamingStation?> GetStationByConnectionIdAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.GamingStations
            .FirstOrDefaultAsync(s => s.ConnectionId == connectionId, cancellationToken);
    }

    // ========================================================================
    // Access Management
    // ========================================================================

    public async Task<IEnumerable<StationAccessGrantResponse>> GetAccessGrantsAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        // Check if user can view access grants
        var permission = await GetUserPermissionAsync(stationId, userId, cancellationToken);
        if (permission == null && station.OwnerId != userId)
            throw new UnauthorizedAccessException("You don't have access to this station.");

        var grants = await _db.StationAccessGrants
            .Include(g => g.User)
            .Include(g => g.GrantedBy)
            .Where(g => g.StationId == stationId)
            .ToListAsync(cancellationToken);

        return grants.Select(ToAccessGrantResponse);
    }

    public async Task<StationAccessGrantResponse> GrantAccessAsync(
        Guid stationId,
        Guid granterId,
        GrantStationAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        // Check if granter has permission
        var granterPermission = await GetUserPermissionAsync(stationId, granterId, cancellationToken);
        var isOwner = station.OwnerId == granterId;

        if (!isOwner && granterPermission != StationPermission.Admin)
            throw new UnauthorizedAccessException("You don't have permission to grant access.");

        // Check if target user exists
        var targetUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        // Check if access already exists
        var existingGrant = await _db.StationAccessGrants
            .FirstOrDefaultAsync(g => g.StationId == stationId && g.UserId == request.UserId, cancellationToken);

        if (existingGrant != null)
            throw new InvalidOperationException("User already has access to this station.");

        // Can't grant higher than your own permission (unless owner)
        if (!isOwner && request.Permission > granterPermission)
            throw new UnauthorizedAccessException("Cannot grant permission higher than your own.");

        var grant = new StationAccessGrant
        {
            StationId = stationId,
            UserId = request.UserId,
            Permission = request.Permission,
            GrantedById = granterId,
            ExpiresAt = request.ExpiresAt
        };

        _db.StationAccessGrants.Add(grant);
        await _db.SaveChangesAsync(cancellationToken);

        // Reload with relationships
        await _db.Entry(grant).Reference(g => g.User).LoadAsync(cancellationToken);
        await _db.Entry(grant).Reference(g => g.GrantedBy).LoadAsync(cancellationToken);

        return ToAccessGrantResponse(grant);
    }

    public async Task<StationAccessGrantResponse> UpdateAccessAsync(
        Guid stationId,
        Guid grantId,
        Guid userId,
        UpdateStationAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        var grant = await _db.StationAccessGrants
            .Include(g => g.User)
            .Include(g => g.GrantedBy)
            .FirstOrDefaultAsync(g => g.Id == grantId && g.StationId == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Access grant not found.");

        // Check permissions
        var userPermission = await GetUserPermissionAsync(stationId, userId, cancellationToken);
        var isOwner = station.OwnerId == userId;

        if (!isOwner && userPermission != StationPermission.Admin)
            throw new UnauthorizedAccessException("You don't have permission to modify access.");

        if (request.Permission.HasValue)
        {
            if (!isOwner && request.Permission > userPermission)
                throw new UnauthorizedAccessException("Cannot grant permission higher than your own.");
            grant.Permission = request.Permission.Value;
        }

        if (request.ExpiresAt.HasValue)
            grant.ExpiresAt = request.ExpiresAt;

        await _db.SaveChangesAsync(cancellationToken);

        return ToAccessGrantResponse(grant);
    }

    public async Task RevokeAccessAsync(
        Guid stationId,
        Guid targetUserId,
        Guid revokerId,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        // Check permissions
        var revokerPermission = await GetUserPermissionAsync(stationId, revokerId, cancellationToken);
        var isOwner = station.OwnerId == revokerId;

        if (!isOwner && revokerPermission != StationPermission.Admin)
            throw new UnauthorizedAccessException("You don't have permission to revoke access.");

        var grant = await _db.StationAccessGrants
            .FirstOrDefaultAsync(g => g.StationId == stationId && g.UserId == targetUserId, cancellationToken);

        if (grant == null)
            return; // Already doesn't have access

        _db.StationAccessGrants.Remove(grant);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StationPermission?> GetUserPermissionAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken);

        if (station == null)
            return null;

        // Owner has implicit full access
        if (station.OwnerId == userId)
            return StationPermission.Admin;

        var grant = await _db.StationAccessGrants
            .FirstOrDefaultAsync(g => g.StationId == stationId &&
                                      g.UserId == userId &&
                                      (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow),
                cancellationToken);

        return grant?.Permission;
    }

    // ========================================================================
    // Session Management
    // ========================================================================

    public async Task<StationSessionResponse?> GetActiveSessionAsync(
        Guid stationId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.StationSessions
            .Include(s => s.ConnectedUsers.Where(u => u.DisconnectedAt == null))
                .ThenInclude(u => u.User)
            .FirstOrDefaultAsync(s => s.StationId == stationId && s.EndedAt == null, cancellationToken);

        if (session == null)
            return null;

        return ToSessionResponse(session);
    }

    public async Task<StationSessionUserResponse> ConnectUserAsync(
        Guid stationId,
        Guid userId,
        string connectionId,
        StationInputMode inputMode,
        CancellationToken cancellationToken = default)
    {
        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        // Verify user has access
        var permission = await GetUserPermissionAsync(stationId, userId, cancellationToken);
        if (permission == null)
            throw new UnauthorizedAccessException("You don't have access to this station.");

        // Validate input mode against permission
        if (inputMode == StationInputMode.FullInput && permission < StationPermission.FullControl)
            inputMode = StationInputMode.Controller;
        if (inputMode == StationInputMode.Controller && permission < StationPermission.Controller)
            inputMode = StationInputMode.ViewOnly;

        var user = await _db.Users.FindAsync([userId], cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        // Get or create active session
        var session = await _db.StationSessions
            .Include(s => s.ConnectedUsers)
            .FirstOrDefaultAsync(s => s.StationId == stationId && s.EndedAt == null, cancellationToken);

        if (session == null)
        {
            session = new StationSession
            {
                StationId = stationId
            };
            _db.StationSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Check if user is already connected (from another device?)
        var existingConnection = session.ConnectedUsers
            .FirstOrDefault(u => u.UserId == userId && u.DisconnectedAt == null);

        if (existingConnection != null)
        {
            // Update existing connection
            existingConnection.ConnectionId = connectionId;
            existingConnection.InputMode = inputMode;
        }
        else
        {
            // Add new connection
            existingConnection = new StationSessionUser
            {
                SessionId = session.Id,
                UserId = userId,
                ConnectionId = connectionId,
                InputMode = inputMode
            };
            session.ConnectedUsers.Add(existingConnection);
        }

        // Update station status
        station.Status = StationStatus.InUse;
        await _db.SaveChangesAsync(cancellationToken);

        return ToSessionUserResponse(existingConnection, user);
    }

    public async Task DisconnectUserAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.StationSessions
            .Include(s => s.ConnectedUsers)
            .FirstOrDefaultAsync(s => s.StationId == stationId && s.EndedAt == null, cancellationToken);

        if (session == null)
            return;

        var userConnection = session.ConnectedUsers
            .FirstOrDefault(u => u.UserId == userId && u.DisconnectedAt == null);

        if (userConnection != null)
        {
            userConnection.DisconnectedAt = DateTime.UtcNow;
        }

        // Check if session is now empty
        var activeUsers = session.ConnectedUsers.Count(u => u.DisconnectedAt == null);
        if (activeUsers == 0)
        {
            session.EndedAt = DateTime.UtcNow;

            // Update station status back to online (if still connected)
            var station = await _db.GamingStations.FindAsync([stationId], cancellationToken);
            if (station != null && station.ConnectionId != null)
            {
                station.Status = StationStatus.Online;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisconnectUserByConnectionIdAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var userConnection = await _db.Set<StationSessionUser>()
            .Include(u => u.Session)
            .FirstOrDefaultAsync(u => u.ConnectionId == connectionId && u.DisconnectedAt == null, cancellationToken);

        if (userConnection == null)
            return;

        await DisconnectUserAsync(userConnection.Session!.StationId, userConnection.UserId, cancellationToken);
    }

    public async Task<StationSessionUserResponse?> AssignPlayerSlotAsync(
        Guid stationId,
        Guid userId,
        int? playerSlot,
        Guid assignerId,
        CancellationToken cancellationToken = default)
    {
        // Validate slot range
        if (playerSlot.HasValue && (playerSlot < 1 || playerSlot > 4))
            throw new InvalidOperationException("Player slot must be between 1 and 4.");

        var station = await _db.GamingStations
            .FirstOrDefaultAsync(s => s.Id == stationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        // Only owner or admin can assign slots
        var assignerPermission = await GetUserPermissionAsync(stationId, assignerId, cancellationToken);
        if (station.OwnerId != assignerId && assignerPermission != StationPermission.Admin)
            throw new UnauthorizedAccessException("You don't have permission to assign player slots.");

        var session = await _db.StationSessions
            .Include(s => s.ConnectedUsers)
                .ThenInclude(u => u.User)
            .FirstOrDefaultAsync(s => s.StationId == stationId && s.EndedAt == null, cancellationToken);

        if (session == null)
            return null;

        var userConnection = session.ConnectedUsers
            .FirstOrDefault(u => u.UserId == userId && u.DisconnectedAt == null);

        if (userConnection == null)
            return null;

        // Check if slot is already taken
        if (playerSlot.HasValue)
        {
            var slotTaken = session.ConnectedUsers
                .Any(u => u.UserId != userId && u.PlayerSlot == playerSlot && u.DisconnectedAt == null);

            if (slotTaken)
                throw new InvalidOperationException($"Player slot {playerSlot} is already taken.");
        }

        userConnection.PlayerSlot = playerSlot;
        await _db.SaveChangesAsync(cancellationToken);

        return ToSessionUserResponse(userConnection, userConnection.User!);
    }

    public async Task UpdateLastInputAsync(
        Guid stationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.StationSessions
            .Include(s => s.ConnectedUsers)
            .FirstOrDefaultAsync(s => s.StationId == stationId && s.EndedAt == null, cancellationToken);

        var userConnection = session?.ConnectedUsers
            .FirstOrDefault(u => u.UserId == userId && u.DisconnectedAt == null);

        if (userConnection != null)
        {
            userConnection.LastInputAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    // ========================================================================
    // Mapping Helpers
    // ========================================================================

    private static GamingStationResponse ToStationResponse(
        GamingStation station,
        Guid viewingUserId,
        bool isOwner,
        StationPermission? permission)
    {
        var activeSession = station.Sessions.FirstOrDefault(s => s.EndedAt == null);
        var connectedCount = activeSession?.ConnectedUsers.Count(u => u.DisconnectedAt == null) ?? 0;

        return new GamingStationResponse(
            Id: station.Id,
            OwnerId: station.OwnerId,
            OwnerUsername: station.Owner?.Username ?? "Unknown",
            OwnerEffectiveDisplayName: station.Owner?.EffectiveDisplayName ?? "Unknown",
            Name: station.Name,
            Description: station.Description,
            Status: station.Status,
            LastSeenAt: station.LastSeenAt,
            CreatedAt: station.CreatedAt,
            ConnectedUserCount: connectedCount,
            IsOwner: isOwner,
            MyPermission: isOwner ? StationPermission.Admin : permission
        );
    }

    private static StationAccessGrantResponse ToAccessGrantResponse(StationAccessGrant grant)
    {
        return new StationAccessGrantResponse(
            Id: grant.Id,
            StationId: grant.StationId,
            UserId: grant.UserId,
            Username: grant.User?.Username ?? "Unknown",
            EffectiveDisplayName: grant.User?.EffectiveDisplayName ?? "Unknown",
            Avatar: grant.User?.AvatarFileName,
            Permission: grant.Permission,
            GrantedById: grant.GrantedById,
            GrantedByUsername: grant.GrantedBy?.Username ?? "Unknown",
            GrantedAt: grant.GrantedAt,
            ExpiresAt: grant.ExpiresAt
        );
    }

    private static StationSessionResponse ToSessionResponse(StationSession session)
    {
        return new StationSessionResponse(
            Id: session.Id,
            StationId: session.StationId,
            StartedAt: session.StartedAt,
            ConnectedUsers: session.ConnectedUsers
                .Where(u => u.DisconnectedAt == null)
                .Select(u => ToSessionUserResponse(u, u.User!))
                .ToList()
        );
    }

    private static StationSessionUserResponse ToSessionUserResponse(StationSessionUser sessionUser, User user)
    {
        return new StationSessionUserResponse(
            UserId: user.Id,
            Username: user.Username,
            EffectiveDisplayName: user.EffectiveDisplayName,
            Avatar: user.AvatarFileName,
            PlayerSlot: sessionUser.PlayerSlot,
            InputMode: sessionUser.InputMode,
            ConnectedAt: sessionUser.ConnectedAt,
            LastInputAt: sessionUser.LastInputAt
        );
    }
}
