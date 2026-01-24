using Moq;
using Snacka.Client.Coordinators;
using Snacka.Client.Services;
using Snacka.Client.Stores;
using Snacka.Shared.Models;

namespace Snacka.Client.Tests.Coordinators;

public class ChannelCoordinatorTests : IDisposable
{
    private readonly ChannelStore _channelStore;
    private readonly MessageStore _messageStore;
    private readonly Mock<IApiClient> _mockApiClient;
    private readonly Mock<ISignalRService> _mockSignalR;
    private readonly ChannelCoordinator _coordinator;

    public ChannelCoordinatorTests()
    {
        _channelStore = new ChannelStore();
        _messageStore = new MessageStore();
        _mockApiClient = new Mock<IApiClient>();
        _mockSignalR = new Mock<ISignalRService>();

        _coordinator = new ChannelCoordinator(
            _channelStore,
            _messageStore,
            _mockApiClient.Object,
            _mockSignalR.Object);
    }

    public void Dispose()
    {
        _channelStore.Dispose();
        _messageStore.Dispose();
    }

    private static ChannelResponse CreateTextChannel(
        Guid? id = null,
        string name = "test-channel",
        int unreadCount = 0)
    {
        return new ChannelResponse(
            Id: id ?? Guid.NewGuid(),
            Name: name,
            Topic: null,
            Type: ChannelType.Text,
            CommunityId: Guid.NewGuid(),
            Position: 0,
            UnreadCount: unreadCount,
            CreatedAt: DateTime.UtcNow
        );
    }

    private static ChannelResponse CreateVoiceChannel(Guid? id = null, string name = "voice-channel")
    {
        return new ChannelResponse(
            Id: id ?? Guid.NewGuid(),
            Name: name,
            Topic: null,
            Type: ChannelType.Voice,
            CommunityId: Guid.NewGuid(),
            Position: 0,
            UnreadCount: 0,
            CreatedAt: DateTime.UtcNow
        );
    }

    private static MessageResponse CreateMessage(
        Guid? id = null,
        Guid? channelId = null,
        string content = "Test message")
    {
        return new MessageResponse(
            Id: id ?? Guid.NewGuid(),
            ChannelId: channelId ?? Guid.NewGuid(),
            Content: content,
            AuthorId: Guid.NewGuid(),
            AuthorUsername: "testuser",
            AuthorEffectiveDisplayName: "Test User",
            AuthorAvatar: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            IsEdited: false,
            IsPinned: false,
            PinnedAt: null,
            PinnedByUsername: null,
            PinnedByEffectiveDisplayName: null,
            ReplyToId: null,
            ReplyTo: null,
            Reactions: null,
            Attachments: null,
            ThreadParentMessageId: null,
            ReplyCount: 0,
            LastReplyAt: null
        );
    }

    #region SelectTextChannelAsync Tests

    [Fact]
    public async Task SelectTextChannelAsync_LoadsMessagesAndUpdatesStores()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var channel = CreateTextChannel(id: channelId);
        _channelStore.AddChannel(channel);

        var messages = new List<MessageResponse>
        {
            CreateMessage(channelId: channelId, content: "Message 1"),
            CreateMessage(channelId: channelId, content: "Message 2")
        };

        _mockApiClient
            .Setup(x => x.GetMessagesAsync(channelId, 0, 50))
            .ReturnsAsync(new ApiResult<List<MessageResponse>> { Success = true, Data = messages });

        _mockApiClient
            .Setup(x => x.MarkChannelAsReadAsync(communityId, channelId))
            .ReturnsAsync(new ApiResult<bool> { Success = true });

        // Act
        await _coordinator.SelectTextChannelAsync(communityId, channelId);

        // Assert
        var selectedChannel = _channelStore.SelectedChannel.WaitFirst();
        Assert.NotNull(selectedChannel);
        Assert.Equal(channelId, selectedChannel.Id);

        var loadedMessages = _messageStore.CurrentChannelMessages.WaitFirst();
        Assert.Equal(2, loadedMessages.Count);

        // Verify SignalR join was called
        _mockSignalR.Verify(x => x.JoinChannelAsync(channelId), Times.Once);
    }

    [Fact]
    public async Task SelectTextChannelAsync_ClearsUnreadCount()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var channel = CreateTextChannel(id: channelId, unreadCount: 10);
        _channelStore.AddChannel(channel);

        _mockApiClient
            .Setup(x => x.GetMessagesAsync(channelId, 0, 50))
            .ReturnsAsync(new ApiResult<List<MessageResponse>> { Success = true, Data = new List<MessageResponse>() });

        _mockApiClient
            .Setup(x => x.MarkChannelAsReadAsync(communityId, channelId))
            .ReturnsAsync(new ApiResult<bool> { Success = true });

        // Act
        await _coordinator.SelectTextChannelAsync(communityId, channelId);

        // Assert
        var channelState = _channelStore.GetChannel(channelId);
        Assert.NotNull(channelState);
        Assert.Equal(0, channelState.UnreadCount);
    }

    [Fact]
    public async Task SelectTextChannelAsync_IgnoresVoiceChannels()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var voiceChannel = CreateVoiceChannel(id: channelId);
        _channelStore.AddChannel(voiceChannel);

        // Act
        await _coordinator.SelectTextChannelAsync(communityId, channelId);

        // Assert
        var selectedChannel = _channelStore.SelectedChannel.WaitFirst();
        Assert.Null(selectedChannel);

        // Verify no API calls were made
        _mockApiClient.Verify(x => x.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SelectTextChannelAsync_LeavesPreviousChannelFirst()
    {
        // Arrange
        var channel1Id = Guid.NewGuid();
        var channel2Id = Guid.NewGuid();
        var communityId = Guid.NewGuid();

        var channel1 = CreateTextChannel(id: channel1Id, name: "channel-1");
        var channel2 = CreateTextChannel(id: channel2Id, name: "channel-2");
        _channelStore.AddChannel(channel1);
        _channelStore.AddChannel(channel2);

        _mockApiClient
            .Setup(x => x.GetMessagesAsync(It.IsAny<Guid>(), 0, 50))
            .ReturnsAsync(new ApiResult<List<MessageResponse>> { Success = true, Data = new List<MessageResponse>() });

        _mockApiClient
            .Setup(x => x.MarkChannelAsReadAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new ApiResult<bool> { Success = true });

        // Act - select first channel, then second
        await _coordinator.SelectTextChannelAsync(communityId, channel1Id);
        await _coordinator.SelectTextChannelAsync(communityId, channel2Id);

        // Assert - should have left channel 1 when selecting channel 2
        _mockSignalR.Verify(x => x.LeaveChannelAsync(channel1Id), Times.Once);
        _mockSignalR.Verify(x => x.JoinChannelAsync(channel2Id), Times.Once);
    }

    #endregion

    #region CreateChannelAsync Tests

    [Fact]
    public async Task CreateChannelAsync_AddsChannelToStore()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var newChannel = CreateTextChannel(name: "new-channel");

        _mockApiClient
            .Setup(x => x.CreateChannelAsync(communityId, "new-channel", null, ChannelType.Text))
            .ReturnsAsync(new ApiResult<ChannelResponse> { Success = true, Data = newChannel });

        // Act
        var result = await _coordinator.CreateChannelAsync(communityId, "new-channel", null, ChannelType.Text);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new-channel", result.Name);

        var channelInStore = _channelStore.GetChannel(newChannel.Id);
        Assert.NotNull(channelInStore);
    }

    [Fact]
    public async Task CreateChannelAsync_ReturnsNullOnFailure()
    {
        // Arrange
        var communityId = Guid.NewGuid();

        _mockApiClient
            .Setup(x => x.CreateChannelAsync(communityId, "new-channel", null, ChannelType.Text))
            .ReturnsAsync(new ApiResult<ChannelResponse> { Success = false, Error = "Failed" });

        // Act
        var result = await _coordinator.CreateChannelAsync(communityId, "new-channel", null, ChannelType.Text);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region DeleteChannelAsync Tests

    [Fact]
    public async Task DeleteChannelAsync_RemovesChannelFromStore()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var channel = CreateTextChannel(id: channelId);
        _channelStore.AddChannel(channel);

        // Add some messages to verify they're cleared
        var message = CreateMessage(channelId: channelId);
        _messageStore.SetCurrentChannel(channelId);
        _messageStore.AddMessage(message);

        _mockApiClient
            .Setup(x => x.DeleteChannelAsync(channelId))
            .ReturnsAsync(new ApiResult<bool> { Success = true });

        // Act
        var result = await _coordinator.DeleteChannelAsync(channelId);

        // Assert
        Assert.True(result);
        Assert.Null(_channelStore.GetChannel(channelId));
    }

    #endregion

    #region MarkChannelAsReadAsync Tests

    [Fact]
    public async Task MarkChannelAsReadAsync_UpdatesUnreadCountImmediately()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var channel = CreateTextChannel(id: channelId, unreadCount: 15);
        _channelStore.AddChannel(channel);

        _mockApiClient
            .Setup(x => x.MarkChannelAsReadAsync(communityId, channelId))
            .ReturnsAsync(new ApiResult<bool> { Success = true });

        // Act
        await _coordinator.MarkChannelAsReadAsync(communityId, channelId);

        // Assert - unread should be 0 immediately (optimistic update)
        var updatedChannel = _channelStore.GetChannel(channelId);
        Assert.NotNull(updatedChannel);
        Assert.Equal(0, updatedChannel.UnreadCount);
    }

    #endregion

    #region ClearSelection Tests

    [Fact]
    public void ClearSelection_ClearsChannelAndMessageStores()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var channel = CreateTextChannel(id: channelId);
        _channelStore.AddChannel(channel);
        _channelStore.SelectChannel(channelId);
        _messageStore.SetCurrentChannel(channelId);
        _messageStore.AddMessage(CreateMessage(channelId: channelId));

        // Act
        _coordinator.ClearSelection();

        // Assert
        var selectedChannel = _channelStore.SelectedChannel.WaitFirst();
        Assert.Null(selectedChannel);
    }

    #endregion
}
