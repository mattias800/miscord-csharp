using Microsoft.EntityFrameworkCore;
using Miscord.Server.Data;
using Miscord.Server.DTOs;
using Miscord.Shared.Models;

namespace Miscord.Server.Services;

public interface IVoiceService
{
    Task<VoiceParticipantResponse> JoinChannelAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);
    Task LeaveChannelAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);
    Task LeaveAllChannelsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<VoiceParticipantResponse?> UpdateStateAsync(Guid channelId, Guid userId, VoiceStateUpdate update, CancellationToken cancellationToken = default);
    Task<IEnumerable<VoiceParticipantResponse>> GetParticipantsAsync(Guid channelId, CancellationToken cancellationToken = default);
    Task<VoiceParticipantResponse?> GetParticipantAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetUserCurrentChannelAsync(Guid userId, CancellationToken cancellationToken = default);
}

public class VoiceService : IVoiceService
{
    private readonly MiscordDbContext _db;
    private readonly ILogger<VoiceService> _logger;

    public VoiceService(MiscordDbContext db, ILogger<VoiceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<VoiceParticipantResponse> JoinChannelAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels
            .Include(c => c.Community)
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Type == ChannelType.Voice, cancellationToken)
            ?? throw new InvalidOperationException("Voice channel not found");

        var isMember = await _db.UserCommunities
            .AnyAsync(uc => uc.UserId == userId && uc.CommunityId == channel.CommunityId, cancellationToken);

        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this community");

        // Leave any existing voice channel first
        await LeaveAllChannelsAsync(userId, cancellationToken);

        var user = await _db.Users.FindAsync([userId], cancellationToken)
            ?? throw new InvalidOperationException("User not found");

        var participant = new VoiceParticipant
        {
            UserId = userId,
            ChannelId = channelId,
            IsMuted = false,
            IsDeafened = false,
            IsScreenSharing = false,
            IsCameraOn = false,
            JoinedAt = DateTime.UtcNow
        };

        _db.VoiceParticipants.Add(participant);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {Username} joined voice channel {ChannelId}", user.Username, channelId);

        return new VoiceParticipantResponse(
            participant.Id,
            participant.UserId,
            user.Username,
            participant.ChannelId,
            participant.IsMuted,
            participant.IsDeafened,
            participant.IsScreenSharing,
            participant.IsCameraOn,
            participant.JoinedAt
        );
    }

    public async Task LeaveChannelAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        var participant = await _db.VoiceParticipants
            .FirstOrDefaultAsync(p => p.ChannelId == channelId && p.UserId == userId, cancellationToken);

        if (participant is not null)
        {
            _db.VoiceParticipants.Remove(participant);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("User {UserId} left voice channel {ChannelId}", userId, channelId);
        }
    }

    public async Task LeaveAllChannelsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var participants = await _db.VoiceParticipants
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        if (participants.Count > 0)
        {
            _db.VoiceParticipants.RemoveRange(participants);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("User {UserId} left all voice channels", userId);
        }
    }

    public async Task<VoiceParticipantResponse?> UpdateStateAsync(Guid channelId, Guid userId, VoiceStateUpdate update, CancellationToken cancellationToken = default)
    {
        var participant = await _db.VoiceParticipants
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.ChannelId == channelId && p.UserId == userId, cancellationToken);

        if (participant is null)
            return null;

        if (update.IsMuted.HasValue)
            participant.IsMuted = update.IsMuted.Value;
        if (update.IsDeafened.HasValue)
            participant.IsDeafened = update.IsDeafened.Value;
        if (update.IsScreenSharing.HasValue)
            participant.IsScreenSharing = update.IsScreenSharing.Value;
        if (update.IsCameraOn.HasValue)
            participant.IsCameraOn = update.IsCameraOn.Value;

        await _db.SaveChangesAsync(cancellationToken);

        return new VoiceParticipantResponse(
            participant.Id,
            participant.UserId,
            participant.User!.Username,
            participant.ChannelId,
            participant.IsMuted,
            participant.IsDeafened,
            participant.IsScreenSharing,
            participant.IsCameraOn,
            participant.JoinedAt
        );
    }

    public async Task<IEnumerable<VoiceParticipantResponse>> GetParticipantsAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        return await _db.VoiceParticipants
            .Include(p => p.User)
            .Where(p => p.ChannelId == channelId)
            .Select(p => new VoiceParticipantResponse(
                p.Id,
                p.UserId,
                p.User!.Username,
                p.ChannelId,
                p.IsMuted,
                p.IsDeafened,
                p.IsScreenSharing,
                p.IsCameraOn,
                p.JoinedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<VoiceParticipantResponse?> GetParticipantAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.VoiceParticipants
            .Include(p => p.User)
            .Where(p => p.ChannelId == channelId && p.UserId == userId)
            .Select(p => new VoiceParticipantResponse(
                p.Id,
                p.UserId,
                p.User!.Username,
                p.ChannelId,
                p.IsMuted,
                p.IsDeafened,
                p.IsScreenSharing,
                p.IsCameraOn,
                p.JoinedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> GetUserCurrentChannelAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var participant = await _db.VoiceParticipants
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return participant?.ChannelId;
    }
}
