using Microsoft.EntityFrameworkCore;
using Miscord.Server.Data;
using Miscord.Server.DTOs;
using Miscord.Shared.Models;

namespace Miscord.Server.Services;

public sealed class CommunityService : ICommunityService
{
    private readonly MiscordDbContext _db;

    public CommunityService(MiscordDbContext db) => _db = db;

    #region Community Operations

    public async Task<IEnumerable<CommunityResponse>> GetUserCommunitiesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var communities = await _db.UserCommunities
            .Include(uc => uc.Community)
            .ThenInclude(c => c!.Owner)
            .Include(uc => uc.Community)
            .ThenInclude(c => c!.UserCommunities)
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.Community!)
            .ToListAsync(cancellationToken);

        return communities.Select(ToCommunityResponse);
    }

    public async Task<IEnumerable<CommunityResponse>> GetDiscoverableCommunitiesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Get all communities that the user is NOT a member of (for discovery/joining)
        var userCommunityIds = await _db.UserCommunities
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.CommunityId)
            .ToListAsync(cancellationToken);

        var communities = await _db.Communities
            .Include(c => c.Owner)
            .Include(c => c.UserCommunities)
            .Where(c => !userCommunityIds.Contains(c.Id))
            .OrderByDescending(c => c.UserCommunities.Count) // Show popular communities first
            .Take(50) // Limit for performance
            .ToListAsync(cancellationToken);

        return communities.Select(ToCommunityResponse);
    }

    public async Task<CommunityResponse> GetCommunityAsync(Guid communityId, CancellationToken cancellationToken = default)
    {
        var community = await _db.Communities
            .Include(c => c.Owner)
            .Include(c => c.UserCommunities)
            .FirstOrDefaultAsync(c => c.Id == communityId, cancellationToken)
            ?? throw new InvalidOperationException("Community not found.");

        return ToCommunityResponse(community);
    }

    public async Task<CommunityResponse> CreateCommunityAsync(Guid ownerId, CreateCommunityRequest request, CancellationToken cancellationToken = default)
    {
        var owner = await _db.Users.FindAsync([ownerId], cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var community = new Community
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = ownerId
        };

        _db.Communities.Add(community);

        // Add owner as first member
        var userCommunity = new UserCommunity
        {
            UserId = ownerId,
            CommunityId = community.Id,
            Role = UserRole.Owner
        };
        _db.UserCommunities.Add(userCommunity);

        // Create default text channel
        var generalChannel = new Channel
        {
            Name = "general",
            CommunityId = community.Id,
            Type = ChannelType.Text,
            Position = 0
        };
        _db.Channels.Add(generalChannel);

        await _db.SaveChangesAsync(cancellationToken);

        // Reload community with relationships for accurate count
        var createdCommunity = await _db.Communities
            .Include(c => c.Owner)
            .Include(c => c.UserCommunities)
            .FirstAsync(c => c.Id == community.Id, cancellationToken);

        return ToCommunityResponse(createdCommunity);
    }

    public async Task<CommunityResponse> UpdateCommunityAsync(Guid communityId, Guid userId, UpdateCommunityRequest request, CancellationToken cancellationToken = default)
    {
        var community = await _db.Communities
            .Include(c => c.Owner)
            .Include(c => c.UserCommunities)
            .FirstOrDefaultAsync(c => c.Id == communityId, cancellationToken)
            ?? throw new InvalidOperationException("Community not found.");

        await EnsureCanManageCommunityAsync(communityId, userId, cancellationToken);

        if (request.Name is not null)
            community.Name = request.Name;
        if (request.Description is not null)
            community.Description = request.Description;
        if (request.Icon is not null)
            community.Icon = request.Icon;

        community.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return ToCommunityResponse(community);
    }

    public async Task DeleteCommunityAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        var community = await _db.Communities.FindAsync([communityId], cancellationToken)
            ?? throw new InvalidOperationException("Community not found.");

        if (community.OwnerId != userId)
            throw new UnauthorizedAccessException("Only the owner can delete the community.");

        _db.Communities.Remove(community);
        await _db.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Channel Operations

    public async Task<IEnumerable<ChannelResponse>> GetChannelsAsync(Guid communityId, CancellationToken cancellationToken = default)
    {
        var channels = await _db.Channels
            .Where(c => c.CommunityId == communityId)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        return channels.Select(ToChannelResponse);
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

    #endregion

    #region Message Operations

    public async Task<IEnumerable<MessageResponse>> GetMessagesAsync(Guid channelId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var messages = await _db.Messages
            .Include(m => m.Author)
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return messages.Select(ToMessageResponse).Reverse();
    }

    public async Task<MessageResponse> SendMessageAsync(Guid channelId, Guid authorId, string content, CancellationToken cancellationToken = default)
    {
        var author = await _db.Users.FindAsync([authorId], cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var channel = await _db.Channels.FindAsync([channelId], cancellationToken)
            ?? throw new InvalidOperationException("Channel not found.");

        var message = new Message
        {
            Content = content,
            AuthorId = authorId,
            ChannelId = channelId
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        message.Author = author;
        return ToMessageResponse(message);
    }

    public async Task<MessageResponse> UpdateMessageAsync(Guid messageId, Guid userId, string content, CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages
            .Include(m => m.Author)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException("Message not found.");

        if (message.AuthorId != userId)
            throw new UnauthorizedAccessException("You can only edit your own messages.");

        message.Content = content;
        message.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return ToMessageResponse(message);
    }

    public async Task DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages
            .Include(m => m.Channel)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException("Message not found.");

        // Allow deletion by author or community admin/owner
        if (message.AuthorId != userId)
        {
            var userCommunity = await _db.UserCommunities
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CommunityId == message.Channel!.CommunityId, cancellationToken);

            if (userCommunity is null || userCommunity.Role == UserRole.Member)
                throw new UnauthorizedAccessException("You cannot delete this message.");
        }

        _db.Messages.Remove(message);
        await _db.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Member Operations

    public async Task<IEnumerable<CommunityMemberResponse>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken = default)
    {
        var members = await _db.UserCommunities
            .Include(uc => uc.User)
            .Where(uc => uc.CommunityId == communityId)
            .ToListAsync(cancellationToken);

        return members.Select(uc => new CommunityMemberResponse(
            uc.UserId,
            uc.User?.Username ?? "Unknown",
            uc.User?.Avatar,
            uc.User?.IsOnline ?? false,
            uc.Role,
            uc.JoinedAt
        ));
    }

    public async Task JoinCommunityAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.UserCommunities
            .AnyAsync(uc => uc.UserId == userId && uc.CommunityId == communityId, cancellationToken);

        if (exists)
            throw new InvalidOperationException("User is already a member of this community.");

        var userCommunity = new UserCommunity
        {
            UserId = userId,
            CommunityId = communityId,
            Role = UserRole.Member
        };

        _db.UserCommunities.Add(userCommunity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveCommunityAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        var community = await _db.Communities.FindAsync([communityId], cancellationToken)
            ?? throw new InvalidOperationException("Community not found.");

        if (community.OwnerId == userId)
            throw new InvalidOperationException("The owner cannot leave the community. Transfer ownership or delete the community.");

        var userCommunity = await _db.UserCommunities
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CommunityId == communityId, cancellationToken)
            ?? throw new InvalidOperationException("User is not a member of this community.");

        _db.UserCommunities.Remove(userCommunity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserCommunities
            .AnyAsync(uc => uc.UserId == userId && uc.CommunityId == communityId, cancellationToken);
    }

    public async Task<CommunityMemberResponse> GetMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        var userCommunity = await _db.UserCommunities
            .Include(uc => uc.User)
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CommunityId == communityId, cancellationToken)
            ?? throw new InvalidOperationException("User is not a member of this community.");

        return new CommunityMemberResponse(
            userCommunity.UserId,
            userCommunity.User?.Username ?? "Unknown",
            userCommunity.User?.Avatar,
            userCommunity.User?.IsOnline ?? false,
            userCommunity.Role,
            userCommunity.JoinedAt
        );
    }

    public async Task<CommunityMemberResponse> UpdateMemberRoleAsync(Guid communityId, Guid targetUserId, Guid requestingUserId, UserRole newRole, CancellationToken cancellationToken = default)
    {
        // Get the requesting user's membership
        var requestingMembership = await _db.UserCommunities
            .FirstOrDefaultAsync(uc => uc.UserId == requestingUserId && uc.CommunityId == communityId, cancellationToken)
            ?? throw new UnauthorizedAccessException("You are not a member of this community.");

        // Only owners can change roles
        if (requestingMembership.Role != UserRole.Owner)
            throw new UnauthorizedAccessException("Only the community owner can change member roles.");

        // Get the target user's membership
        var targetMembership = await _db.UserCommunities
            .Include(uc => uc.User)
            .FirstOrDefaultAsync(uc => uc.UserId == targetUserId && uc.CommunityId == communityId, cancellationToken)
            ?? throw new InvalidOperationException("Target user is not a member of this community.");

        // Cannot change owner's role
        if (targetMembership.Role == UserRole.Owner)
            throw new InvalidOperationException("Cannot change the owner's role.");

        // Cannot promote someone to owner
        if (newRole == UserRole.Owner)
            throw new InvalidOperationException("Cannot promote a member to owner. Use transfer ownership instead.");

        targetMembership.Role = newRole;
        await _db.SaveChangesAsync(cancellationToken);

        return new CommunityMemberResponse(
            targetMembership.UserId,
            targetMembership.User?.Username ?? "Unknown",
            targetMembership.User?.Avatar,
            targetMembership.User?.IsOnline ?? false,
            targetMembership.Role,
            targetMembership.JoinedAt
        );
    }

    public async Task TransferOwnershipAsync(Guid communityId, Guid newOwnerId, Guid currentOwnerId, CancellationToken cancellationToken = default)
    {
        // Get the community
        var community = await _db.Communities.FindAsync([communityId], cancellationToken)
            ?? throw new InvalidOperationException("Community not found.");

        // Verify the current user is the owner
        if (community.OwnerId != currentOwnerId)
            throw new UnauthorizedAccessException("Only the current owner can transfer ownership.");

        // Can't transfer to yourself
        if (newOwnerId == currentOwnerId)
            throw new InvalidOperationException("Cannot transfer ownership to yourself.");

        // Get the new owner's membership
        var newOwnerMembership = await _db.UserCommunities
            .FirstOrDefaultAsync(uc => uc.UserId == newOwnerId && uc.CommunityId == communityId, cancellationToken)
            ?? throw new InvalidOperationException("New owner must be a member of the community.");

        // Get the current owner's membership
        var currentOwnerMembership = await _db.UserCommunities
            .FirstOrDefaultAsync(uc => uc.UserId == currentOwnerId && uc.CommunityId == communityId, cancellationToken)
            ?? throw new InvalidOperationException("Current owner membership not found.");

        // Update community owner
        community.OwnerId = newOwnerId;
        community.UpdatedAt = DateTime.UtcNow;

        // Update roles
        newOwnerMembership.Role = UserRole.Owner;
        currentOwnerMembership.Role = UserRole.Admin; // Former owner becomes admin

        await _db.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Private Helpers

    private async Task EnsureCanManageCommunityAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        var userCommunity = await _db.UserCommunities
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CommunityId == communityId, cancellationToken);

        if (userCommunity is null)
            throw new UnauthorizedAccessException("User is not a member of this community.");

        if (userCommunity.Role != UserRole.Owner && userCommunity.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("You don't have permission to manage this community.");
    }

    private static CommunityResponse ToCommunityResponse(Community c) => new(
        c.Id,
        c.Name,
        c.Description,
        c.Icon,
        c.OwnerId,
        c.Owner?.Username ?? "Unknown",
        c.CreatedAt,
        c.UserCommunities.Count
    );

    private static ChannelResponse ToChannelResponse(Channel c) => new(
        c.Id,
        c.Name,
        c.Topic,
        c.CommunityId,
        c.Type,
        c.Position,
        c.CreatedAt
    );

    private static MessageResponse ToMessageResponse(Message m) => new(
        m.Id,
        m.Content,
        m.AuthorId,
        m.Author?.Username ?? "Unknown",
        m.Author?.Avatar,
        m.ChannelId,
        m.CreatedAt,
        m.UpdatedAt,
        m.IsEdited
    );

    #endregion
}
