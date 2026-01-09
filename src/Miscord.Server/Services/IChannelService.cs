using Miscord.Server.DTOs;
using Miscord.Shared.Models;

namespace Miscord.Server.Services;

public interface IChannelService
{
    Task<IEnumerable<ChannelResponse>> GetChannelsAsync(Guid communityId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ChannelResponse>> GetChannelsAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);
    Task<ChannelResponse> GetChannelAsync(Guid channelId, CancellationToken cancellationToken = default);
    Task<ChannelResponse> CreateChannelAsync(Guid communityId, Guid userId, CreateChannelRequest request, CancellationToken cancellationToken = default);
    Task<ChannelResponse> UpdateChannelAsync(Guid channelId, Guid userId, UpdateChannelRequest request, CancellationToken cancellationToken = default);
    Task DeleteChannelAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ChannelResponse>> ReorderChannelsAsync(Guid communityId, Guid userId, List<Guid> channelIds, CancellationToken cancellationToken = default);
    Task MarkChannelAsReadAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);
}
