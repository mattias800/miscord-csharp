using Snacka.Client.Services;
using Snacka.Shared.Models;

namespace Snacka.Client.Tests.Integration;

/// <summary>
/// Integration tests for channel-related SignalR event flows.
/// Tests that SignalR events correctly flow through SignalREventDispatcher to update ChannelStore.
/// </summary>
public class ChannelFlowIntegrationTests : ClientIntegrationTestBase
{
    [Fact]
    public void ChannelCreated_AddsChannelToStore()
    {
        // Arrange
        var channel = CreateChannel(name: "new-channel", type: ChannelType.Text);

        // Act
        SignalR.RaiseChannelCreated(channel);

        // Assert
        var storedChannel = ChannelStore.GetChannel(channel.Id);
        Assert.NotNull(storedChannel);
        Assert.Equal("new-channel", storedChannel.Name);
        Assert.Equal(ChannelType.Text, storedChannel.Type);
    }

    [Fact]
    public void ChannelCreated_MultipleChannels_AllAddedToStore()
    {
        // Arrange & Act
        var channel1 = CreateChannel(name: "channel-1");
        var channel2 = CreateChannel(name: "channel-2");
        var channel3 = CreateChannel(name: "channel-3");

        SignalR.RaiseChannelCreated(channel1);
        SignalR.RaiseChannelCreated(channel2);
        SignalR.RaiseChannelCreated(channel3);

        // Assert
        Assert.NotNull(ChannelStore.GetChannel(channel1.Id));
        Assert.NotNull(ChannelStore.GetChannel(channel2.Id));
        Assert.NotNull(ChannelStore.GetChannel(channel3.Id));

        var allChannels = ChannelStore.GetAllChannels();
        Assert.Equal(3, allChannels.Count);
    }

    [Fact]
    public void ChannelUpdated_UpdatesExistingChannel()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var originalChannel = CreateChannel(id: channelId, name: "original-name");
        SetupChannel(originalChannel);

        var updatedChannel = CreateChannel(id: channelId, name: "updated-name");

        // Act
        SignalR.RaiseChannelUpdated(updatedChannel);

        // Assert
        var storedChannel = ChannelStore.GetChannel(channelId);
        Assert.NotNull(storedChannel);
        Assert.Equal("updated-name", storedChannel.Name);
    }

    [Fact]
    public void ChannelDeleted_RemovesChannelFromStore()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var channel = CreateChannel(id: channelId);
        SetupChannel(channel);

        // Verify channel exists
        Assert.NotNull(ChannelStore.GetChannel(channelId));

        var deletedEvent = new ChannelDeletedEvent(
            ChannelId: channelId
        );

        // Act
        SignalR.RaiseChannelDeleted(deletedEvent);

        // Assert
        Assert.Null(ChannelStore.GetChannel(channelId));
    }

    [Fact]
    public void ChannelsReordered_UpdatesChannelPositions()
    {
        // Arrange
        var channel1 = CreateChannel(name: "channel-1", position: 0);
        var channel2 = CreateChannel(name: "channel-2", position: 1);
        var channel3 = CreateChannel(name: "channel-3", position: 2);

        ChannelStore.SetChannels(new[] { channel1, channel2, channel3 });

        // New order: channel3, channel1, channel2
        var reorderedChannels = new List<ChannelResponse>
        {
            channel3 with { Position = 0 },
            channel1 with { Position = 1 },
            channel2 with { Position = 2 }
        };

        var reorderedEvent = new ChannelsReorderedEvent(
            CommunityId: TestCommunityId,
            Channels: reorderedChannels
        );

        // Act
        SignalR.RaiseChannelsReordered(reorderedEvent);

        // Assert
        var storedChannel1 = ChannelStore.GetChannel(channel1.Id);
        var storedChannel2 = ChannelStore.GetChannel(channel2.Id);
        var storedChannel3 = ChannelStore.GetChannel(channel3.Id);

        Assert.Equal(1, storedChannel1?.Position);
        Assert.Equal(2, storedChannel2?.Position);
        Assert.Equal(0, storedChannel3?.Position);
    }

    [Fact]
    public void ChannelCreated_VoiceChannel_AddsToStore()
    {
        // Arrange
        var voiceChannel = CreateChannel(name: "Voice Chat", type: ChannelType.Voice);

        // Act
        SignalR.RaiseChannelCreated(voiceChannel);

        // Assert
        var storedChannel = ChannelStore.GetChannel(voiceChannel.Id);
        Assert.NotNull(storedChannel);
        Assert.Equal(ChannelType.Voice, storedChannel.Type);
    }

    [Fact]
    public void ChannelDeleted_DoesNotAffectOtherChannels()
    {
        // Arrange
        var channel1 = CreateChannel(name: "channel-1");
        var channel2 = CreateChannel(name: "channel-2");
        ChannelStore.SetChannels(new[] { channel1, channel2 });

        // Act - Delete only channel1
        SignalR.RaiseChannelDeleted(new ChannelDeletedEvent(channel1.Id));

        // Assert
        Assert.Null(ChannelStore.GetChannel(channel1.Id));
        Assert.NotNull(ChannelStore.GetChannel(channel2.Id));
    }
}

/// <summary>
/// Integration tests for presence-related SignalR event flows.
/// Tests that SignalR events correctly flow through SignalREventDispatcher to update PresenceStore.
/// </summary>
public class PresenceFlowIntegrationTests : ClientIntegrationTestBase
{
    [Fact]
    public void UserOnline_AddsUserToOnlineSet()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var presenceEvent = CreatePresenceEvent(userId: userId, username: "Alice", isOnline: true);

        // Act
        SignalR.RaiseUserOnline(presenceEvent);

        // Assert
        Assert.True(PresenceStore.IsUserOnline(userId));
    }

    [Fact]
    public void UserOffline_RemovesUserFromOnlineSet()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // First, make user online
        SignalR.RaiseUserOnline(CreatePresenceEvent(userId: userId, isOnline: true));
        Assert.True(PresenceStore.IsUserOnline(userId));

        // Act
        SignalR.RaiseUserOffline(CreatePresenceEvent(userId: userId, isOnline: false));

        // Assert
        Assert.False(PresenceStore.IsUserOnline(userId));
    }

    [Fact]
    public void MultipleUsersOnline_AllTracked()
    {
        // Arrange & Act
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        SignalR.RaiseUserOnline(CreatePresenceEvent(userId: user1, username: "Alice", isOnline: true));
        SignalR.RaiseUserOnline(CreatePresenceEvent(userId: user2, username: "Bob", isOnline: true));
        SignalR.RaiseUserOnline(CreatePresenceEvent(userId: user3, username: "Charlie", isOnline: true));

        // Assert
        Assert.True(PresenceStore.IsUserOnline(user1));
        Assert.True(PresenceStore.IsUserOnline(user2));
        Assert.True(PresenceStore.IsUserOnline(user3));
    }

    [Fact]
    public void ConnectionStateChanged_UpdatesStore()
    {
        // Arrange & Act
        SignalR.RaiseConnectionStateChanged(ConnectionState.Reconnecting);

        // Assert - Use observable subscription to verify
        ConnectionState? actualState = null;
        using var subscription = PresenceStore.ConnectionStatus.Subscribe(state => actualState = state);
        Assert.Equal(ConnectionState.Reconnecting, actualState);
    }
}

/// <summary>
/// Integration tests for typing indicator SignalR event flows.
/// </summary>
public class TypingFlowIntegrationTests : ClientIntegrationTestBase
{
    [Fact]
    public void UserTyping_AddsTypingIndicator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var typingEvent = CreateTypingEvent(userId: userId, channelId: TestChannelId, username: "Alice");

        // Act
        SignalR.RaiseUserTyping(typingEvent);

        // Assert
        var typingUsers = TypingStore.GetTypingUsersSync(TestChannelId);
        Assert.Single(typingUsers);
        Assert.Equal("Alice", typingUsers.First().Username);
    }

    [Fact]
    public void MultipleUsersTyping_AllTracked()
    {
        // Arrange & Act
        SignalR.RaiseUserTyping(CreateTypingEvent(username: "Alice"));
        SignalR.RaiseUserTyping(CreateTypingEvent(username: "Bob"));

        // Assert
        var typingUsers = TypingStore.GetTypingUsersSync(TestChannelId);
        Assert.Equal(2, typingUsers.Count);
        Assert.Contains(typingUsers, t => t.Username == "Alice");
        Assert.Contains(typingUsers, t => t.Username == "Bob");
    }

    [Fact]
    public void TypingIndicators_DifferentChannels_AreIsolated()
    {
        // Arrange
        var channel1 = Guid.NewGuid();
        var channel2 = Guid.NewGuid();

        // Act
        SignalR.RaiseUserTyping(CreateTypingEvent(channelId: channel1, username: "Alice"));
        SignalR.RaiseUserTyping(CreateTypingEvent(channelId: channel2, username: "Bob"));

        // Assert
        var channel1Typing = TypingStore.GetTypingUsersSync(channel1);
        var channel2Typing = TypingStore.GetTypingUsersSync(channel2);

        Assert.Single(channel1Typing);
        Assert.Equal("Alice", channel1Typing.First().Username);

        Assert.Single(channel2Typing);
        Assert.Equal("Bob", channel2Typing.First().Username);
    }
}
