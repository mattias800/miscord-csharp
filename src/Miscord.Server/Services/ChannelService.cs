using Microsoft.EntityFrameworkCore;
using Miscord.Server.Data;
using Miscord.Server.DTOs;
using Miscord.Shared.Models;

namespace Miscord.Server.Services;

public sealed class ChannelService : IChannelService
{
    private readonly MiscordDbContext _db;

    public ChannelService(MiscordDbContext db) => _db = db;

    public async Task<IEnumerable<ChannelResponse>> GetChannelsAsync(Guid communityId, CancellationToken cancellationToken = default)
    {
        var channels = await _db.Channels
            .Where(c => c.CommunityId == communityId)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        return channels.Select(c => ToChannelResponse(c, 0));
    }

    public async Task<IEnumerable<ChannelResponse>> GetChannelsAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        var channels = await _db.Channels
            .Where(c => c.CommunityId == communityId)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        var result = new List<ChannelResponse>();
        foreach (var channel in channels)
        {
            var unreadCount = await GetUnreadCountAsync(channel.Id, userId, cancellationToken);
            result.Add(ToChannelResponse(channel, unreadCount));
        }
        return result;
    }

    public async Task<ChannelResponse> GetChannelAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels.FindAsync([channelId], cancellationToken)
            ?? throw new InvalidOperationException("Channel not found.");

        return ToChannelResponse(channel);
    }

    public async Task<ChannelResponse> CreateChannelAsync(Guid communityId, Guid userId, CreateChannelRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageCommunityAsync(communityId, userId, cancellationToken);

        var maxPosition = await _db.Channels
            .Where(c => c.CommunityId == communityId)
            .MaxAsync(c => (int?)c.Position, cancellationToken) ?? -1;

        var channel = new Channel
        {
            Name = request.Name,
            Topic = request.Topic,
            CommunityId = communityId,
            Type = request.Type,
            Position = maxPosition + 1
        };

        _db.Channels.Add(channel);
        await _db.SaveChangesAsync(cancellationToken);

        return ToChannelResponse(channel);
    }

    public async Task<ChannelResponse> UpdateChannelAsync(Guid channelId, Guid userId, UpdateChannelRequest request, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels.FindAsync([channelId], cancellationToken)
            ?? throw new InvalidOperationException("Channel not found.");

        await EnsureCanManageCommunityAsync(channel.CommunityId, userId, cancellationToken);

        if (request.Name is not null)
            channel.Name = request.Name;
        if (request.Topic is not null)
            channel.Topic = request.Topic;
        if (request.Position.HasValue)
            channel.Position = request.Position.Value;

        channel.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return ToChannelResponse(channel);
    }

    public async Task DeleteChannelAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels.FindAsync([channelId], cancellationToken)
            ?? throw new InvalidOperationException("Channel not found.");

        await EnsureCanManageCommunityAsync(channel.CommunityId, userId, cancellationToken);

        _db.Channels.Remove(channel);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkChannelAsReadAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels.FindAsync([channelId], cancellationToken)
            ?? throw new InvalidOperationException("Channel not found.");

        // Get the latest message in the channel
        var latestMessage = await _db.Messages
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Find or create the read state
        var readState = await _db.ChannelReadStates
            .FirstOrDefaultAsync(crs => crs.UserId == userId && crs.ChannelId == channelId, cancellationToken);

        if (readState is null)
        {
            readState = new ChannelReadState
            {
                UserId = userId,
                ChannelId = channelId,
                LastReadMessageId = latestMessage?.Id,
                LastReadAt = DateTime.UtcNow
            };
            _db.ChannelReadStates.Add(readState);
        }
        else
        {
            readState.LastReadMessageId = latestMessage?.Id;
            readState.LastReadAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        var readState = await _db.ChannelReadStates
            .FirstOrDefaultAsync(crs => crs.UserId == userId && crs.ChannelId == channelId, cancellationToken);

        if (readState is null || readState.LastReadMessageId is null)
        {
            // Never read this channel - count all messages
            return await _db.Messages
                .CountAsync(m => m.ChannelId == channelId, cancellationToken);
        }

        // Get the timestamp of the last read message
        var lastReadMessage = await _db.Messages.FindAsync([readState.LastReadMessageId], cancellationToken);
        if (lastReadMessage is null)
        {
            // Message was deleted - count all messages
            return await _db.Messages
                .CountAsync(m => m.ChannelId == channelId, cancellationToken);
        }

        // Count messages created after the last read message
        return await _db.Messages
            .CountAsync(m => m.ChannelId == channelId && m.CreatedAt > lastReadMessage.CreatedAt, cancellationToken);
    }

    private async Task EnsureCanManageCommunityAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        var userCommunity = await _db.UserCommunities
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CommunityId == communityId, cancellationToken);

        if (userCommunity is null)
            throw new UnauthorizedAccessException("User is not a member of this community.");

        if (userCommunity.Role != UserRole.Owner && userCommunity.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("You don't have permission to manage this community.");
    }

    private static ChannelResponse ToChannelResponse(Channel c, int unreadCount = 0) => new(
        c.Id,
        c.Name,
        c.Topic,
        c.CommunityId,
        c.Type,
        c.Position,
        c.CreatedAt,
        unreadCount
    );
}
