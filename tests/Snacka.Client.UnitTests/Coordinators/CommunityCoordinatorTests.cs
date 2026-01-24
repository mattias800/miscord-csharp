using Moq;
using Snacka.Client.Coordinators;
using Snacka.Client.Services;
using Snacka.Client.Stores;
using Snacka.Shared.Models;

namespace Snacka.Client.Tests.Coordinators;

public class CommunityCoordinatorTests : IDisposable
{
    private readonly CommunityStore _communityStore;
    private readonly ChannelStore _channelStore;
    private readonly MessageStore _messageStore;
    private readonly VoiceStore _voiceStore;
    private readonly Mock<IApiClient> _mockApiClient;
    private readonly Mock<ISignalRService> _mockSignalR;
    private readonly CommunityCoordinator _coordinator;

    public CommunityCoordinatorTests()
    {
        _communityStore = new CommunityStore();
        _channelStore = new ChannelStore();
        _messageStore = new MessageStore();
        _voiceStore = new VoiceStore();
        _mockApiClient = new Mock<IApiClient>();
        _mockSignalR = new Mock<ISignalRService>();

        _coordinator = new CommunityCoordinator(
            _communityStore,
            _channelStore,
            _messageStore,
            _voiceStore,
            _mockApiClient.Object,
            _mockSignalR.Object);
    }

    public void Dispose()
    {
        _communityStore.Dispose();
        _channelStore.Dispose();
        _messageStore.Dispose();
        _voiceStore.Dispose();
    }

    private static CommunityResponse CreateCommunity(
        Guid? id = null,
        string name = "Test Community",
        int memberCount = 5)
    {
        return new CommunityResponse(
            Id: id ?? Guid.NewGuid(),
            Name: name,
            Description: null,
            Icon: null,
            OwnerId: Guid.NewGuid(),
            OwnerUsername: "owner",
            OwnerEffectiveDisplayName: "Owner",
            CreatedAt: DateTime.UtcNow,
            MemberCount: memberCount
        );
    }

    private static ChannelResponse CreateChannel(Guid? id = null, Guid? communityId = null, string name = "test-channel")
    {
        return new ChannelResponse(
            Id: id ?? Guid.NewGuid(),
            Name: name,
            Topic: null,
            Type: ChannelType.Text,
            CommunityId: communityId ?? Guid.NewGuid(),
            Position: 0,
            UnreadCount: 0,
            CreatedAt: DateTime.UtcNow
        );
    }

    private static CommunityMemberResponse CreateMember(
        Guid? userId = null,
        string username = "member",
        UserRole role = UserRole.Member,
        bool isOnline = true)
    {
        return new CommunityMemberResponse(
            UserId: userId ?? Guid.NewGuid(),
            Username: username,
            DisplayName: null,
            DisplayNameOverride: null,
            EffectiveDisplayName: username,
            Avatar: null,
            IsOnline: isOnline,
            Role: role,
            JoinedAt: DateTime.UtcNow
        );
    }

    #region LoadCommunitiesAsync Tests

    [Fact]
    public async Task LoadCommunitiesAsync_PopulatesStore()
    {
        // Arrange
        var communities = new List<CommunityResponse>
        {
            CreateCommunity(name: "Community 1"),
            CreateCommunity(name: "Community 2")
        };

        _mockApiClient
            .Setup(x => x.GetCommunitiesAsync())
            .ReturnsAsync(new ApiResult<List<CommunityResponse>> { Success = true, Data = communities });

        // Act
        var result = await _coordinator.LoadCommunitiesAsync();

        // Assert
        Assert.True(result);
        var items = _communityStore.Items.WaitFirst();
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task LoadCommunitiesAsync_ReturnsFalseOnFailure()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.GetCommunitiesAsync())
            .ReturnsAsync(new ApiResult<List<CommunityResponse>> { Success = false, Error = "Failed" });

        // Act
        var result = await _coordinator.LoadCommunitiesAsync();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region SelectCommunityAsync Tests

    [Fact]
    public async Task SelectCommunityAsync_UpdatesSelectionAndLoadsData()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var community = CreateCommunity(id: communityId, name: "Test Community");
        _communityStore.AddCommunity(community);

        var channels = new List<ChannelResponse>
        {
            CreateChannel(communityId: communityId, name: "general"),
            CreateChannel(communityId: communityId, name: "random")
        };

        var members = new List<CommunityMemberResponse>
        {
            CreateMember(username: "alice"),
            CreateMember(username: "bob")
        };

        _mockApiClient
            .Setup(x => x.GetChannelsAsync(communityId))
            .ReturnsAsync(new ApiResult<List<ChannelResponse>> { Success = true, Data = channels });

        _mockApiClient
            .Setup(x => x.GetMembersAsync(communityId))
            .ReturnsAsync(new ApiResult<List<CommunityMemberResponse>> { Success = true, Data = members });

        // Act
        await _coordinator.SelectCommunityAsync(communityId);

        // Assert
        var selectedId = _communityStore.SelectedCommunityId.WaitFirst();
        Assert.Equal(communityId, selectedId);

        var loadedChannels = _channelStore.Items.WaitFirst();
        Assert.Equal(2, loadedChannels.Count);

        _mockSignalR.Verify(x => x.JoinServerAsync(communityId), Times.Once);
    }

    [Fact]
    public async Task SelectCommunityAsync_LeavesPreviousCommunity()
    {
        // Arrange
        var community1Id = Guid.NewGuid();
        var community2Id = Guid.NewGuid();

        var community1 = CreateCommunity(id: community1Id, name: "Community 1");
        var community2 = CreateCommunity(id: community2Id, name: "Community 2");
        _communityStore.AddCommunity(community1);
        _communityStore.AddCommunity(community2);

        _mockApiClient
            .Setup(x => x.GetChannelsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new ApiResult<List<ChannelResponse>> { Success = true, Data = new List<ChannelResponse>() });

        _mockApiClient
            .Setup(x => x.GetMembersAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new ApiResult<List<CommunityMemberResponse>> { Success = true, Data = new List<CommunityMemberResponse>() });

        // Act
        await _coordinator.SelectCommunityAsync(community1Id);
        await _coordinator.SelectCommunityAsync(community2Id);

        // Assert
        _mockSignalR.Verify(x => x.LeaveServerAsync(community1Id), Times.Once);
        _mockSignalR.Verify(x => x.JoinServerAsync(community2Id), Times.Once);
    }

    [Fact]
    public async Task SelectCommunityAsync_ClearsPreviousChannelsAndMessages()
    {
        // Arrange
        var community1Id = Guid.NewGuid();
        var community2Id = Guid.NewGuid();

        var community1 = CreateCommunity(id: community1Id);
        var community2 = CreateCommunity(id: community2Id);
        _communityStore.AddCommunity(community1);
        _communityStore.AddCommunity(community2);

        // Set up community 1's channels
        var community1Channels = new List<ChannelResponse> { CreateChannel(communityId: community1Id, name: "channel-1") };
        var community2Channels = new List<ChannelResponse> { CreateChannel(communityId: community2Id, name: "channel-2") };

        _mockApiClient
            .Setup(x => x.GetChannelsAsync(community1Id))
            .ReturnsAsync(new ApiResult<List<ChannelResponse>> { Success = true, Data = community1Channels });

        _mockApiClient
            .Setup(x => x.GetChannelsAsync(community2Id))
            .ReturnsAsync(new ApiResult<List<ChannelResponse>> { Success = true, Data = community2Channels });

        _mockApiClient
            .Setup(x => x.GetMembersAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new ApiResult<List<CommunityMemberResponse>> { Success = true, Data = new List<CommunityMemberResponse>() });

        // Act - select first, then switch to second
        await _coordinator.SelectCommunityAsync(community1Id);

        var channelsAfterFirst = _channelStore.Items.WaitFirst();
        Assert.Single(channelsAfterFirst);
        Assert.Equal("channel-1", channelsAfterFirst.First().Name);

        await _coordinator.SelectCommunityAsync(community2Id);

        // Assert - should only have community 2's channels
        var channelsAfterSecond = _channelStore.Items.WaitFirst();
        Assert.Single(channelsAfterSecond);
        Assert.Equal("channel-2", channelsAfterSecond.First().Name);
    }

    #endregion

    #region CreateCommunityAsync Tests

    [Fact]
    public async Task CreateCommunityAsync_AddsCommunityToStore()
    {
        // Arrange
        var newCommunity = CreateCommunity(name: "New Community");

        _mockApiClient
            .Setup(x => x.CreateCommunityAsync("New Community", null))
            .ReturnsAsync(new ApiResult<CommunityResponse> { Success = true, Data = newCommunity });

        // Act
        var result = await _coordinator.CreateCommunityAsync("New Community", null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Community", result.Name);

        var communityInStore = _communityStore.GetCommunity(newCommunity.Id);
        Assert.NotNull(communityInStore);
    }

    [Fact]
    public async Task CreateCommunityAsync_ReturnsNullOnFailure()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.CreateCommunityAsync("New Community", null))
            .ReturnsAsync(new ApiResult<CommunityResponse> { Success = false, Error = "Failed" });

        // Act
        var result = await _coordinator.CreateCommunityAsync("New Community", null);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region UpdateMemberRoleAsync Tests

    [Fact]
    public async Task UpdateMemberRoleAsync_UpdatesRoleInStore()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var community = CreateCommunity(id: communityId);
        _communityStore.AddCommunity(community);
        _communityStore.SetMembers(communityId, new[] { CreateMember(userId: userId, role: UserRole.Member) });

        _mockApiClient
            .Setup(x => x.UpdateMemberRoleAsync(communityId, userId, UserRole.Admin))
            .ReturnsAsync(new ApiResult<CommunityMemberResponse> { Success = true });

        // Act
        var result = await _coordinator.UpdateMemberRoleAsync(communityId, userId, UserRole.Admin);

        // Assert
        Assert.True(result);
        var member = _communityStore.GetMember(userId);
        Assert.NotNull(member);
        Assert.Equal(UserRole.Admin, member.Role);
    }

    #endregion

    #region ClearSelection Tests

    [Fact]
    public void ClearSelection_ClearsAllRelatedStores()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var community = CreateCommunity(id: communityId);
        _communityStore.AddCommunity(community);
        _communityStore.SelectCommunity(communityId);
        _channelStore.AddChannel(CreateChannel(communityId: communityId));

        // Act
        _coordinator.ClearSelection();

        // Assert
        var selectedId = _communityStore.SelectedCommunityId.WaitFirst();
        Assert.Null(selectedId);

        var channels = _channelStore.Items.WaitFirst();
        Assert.Empty(channels);
    }

    #endregion
}
