using Miscord.Server.DTOs;
using Miscord.Shared.Models;

namespace Miscord.Server.Services;

public interface ICommunityService
{
    // Community operations
    Task<IEnumerable<CommunityResponse>> GetUserCommunitiesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CommunityResponse>> GetDiscoverableCommunitiesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CommunityResponse> GetCommunityAsync(Guid communityId, CancellationToken cancellationToken = default);
    Task<CommunityResponse> CreateCommunityAsync(Guid ownerId, CreateCommunityRequest request, CancellationToken cancellationToken = default);
    Task<CommunityResponse> UpdateCommunityAsync(Guid communityId, Guid userId, UpdateCommunityRequest request, CancellationToken cancellationToken = default);
    Task DeleteCommunityAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);

    // Channel operations
    Task<IEnumerable<ChannelResponse>> GetChannelsAsync(Guid communityId, CancellationToken cancellationToken = default);
    Task<ChannelResponse> GetChannelAsync(Guid channelId, CancellationToken cancellationToken = default);
    Task<ChannelResponse> CreateChannelAsync(Guid communityId, Guid userId, CreateChannelRequest request, CancellationToken cancellationToken = default);
    Task<ChannelResponse> UpdateChannelAsync(Guid channelId, Guid userId, UpdateChannelRequest request, CancellationToken cancellationToken = default);
    Task DeleteChannelAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);

    // Message operations
    Task<IEnumerable<MessageResponse>> GetMessagesAsync(Guid channelId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<MessageResponse> SendMessageAsync(Guid channelId, Guid authorId, string content, CancellationToken cancellationToken = default);
    Task<MessageResponse> UpdateMessageAsync(Guid messageId, Guid userId, string content, CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    // Member operations
    Task<IEnumerable<CommunityMemberResponse>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken = default);
    Task<CommunityMemberResponse> GetMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);
    Task JoinCommunityAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);
    Task LeaveCommunityAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);
    Task<CommunityMemberResponse> UpdateMemberRoleAsync(Guid communityId, Guid targetUserId, Guid requestingUserId, UserRole newRole, CancellationToken cancellationToken = default);
    Task TransferOwnershipAsync(Guid communityId, Guid newOwnerId, Guid currentOwnerId, CancellationToken cancellationToken = default);
}
