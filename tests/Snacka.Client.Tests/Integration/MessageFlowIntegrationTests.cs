using Snacka.Client.Services;

namespace Snacka.Client.Tests.Integration;

/// <summary>
/// Integration tests for message-related SignalR event flows.
/// Tests that SignalR events correctly flow through SignalREventDispatcher to update MessageStore.
/// </summary>
public class MessageFlowIntegrationTests : ClientIntegrationTestBase
{
    [Fact]
    public void MessageReceived_AddsMessageToStore()
    {
        // Arrange
        var channel = CreateChannel(id: TestChannelId);
        SetupChannel(channel);
        var message = CreateMessage(channelId: TestChannelId, content: "Hello, world!");

        // Act - Simulate SignalR event
        SignalR.RaiseMessageReceived(message);

        // Assert - Verify message is in store
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        Assert.Single(messages);
        Assert.Equal("Hello, world!", messages.First().Content);
        Assert.Equal(message.Id, messages.First().Id);
    }

    [Fact]
    public void MessageReceived_MultipleMessages_AllAddedToStore()
    {
        // Arrange
        var channel = CreateChannel(id: TestChannelId);
        SetupChannel(channel);
        var message1 = CreateMessage(channelId: TestChannelId, content: "First message");
        var message2 = CreateMessage(channelId: TestChannelId, content: "Second message");
        var message3 = CreateMessage(channelId: TestChannelId, content: "Third message");

        // Act - Simulate multiple SignalR events
        SignalR.RaiseMessageReceived(message1);
        SignalR.RaiseMessageReceived(message2);
        SignalR.RaiseMessageReceived(message3);

        // Assert - All messages in store
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        Assert.Equal(3, messages.Count);
        Assert.Contains(messages, m => m.Content == "First message");
        Assert.Contains(messages, m => m.Content == "Second message");
        Assert.Contains(messages, m => m.Content == "Third message");
    }

    [Fact]
    public void MessageEdited_UpdatesExistingMessage()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var originalMessage = CreateMessage(id: messageId, channelId: TestChannelId, content: "Original content");
        SetupMessages(TestChannelId, originalMessage);

        var editedMessage = CreateMessage(id: messageId, channelId: TestChannelId, content: "Edited content");

        // Act - Simulate edit event
        SignalR.RaiseMessageEdited(editedMessage);

        // Assert - Message content is updated
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        Assert.Single(messages);
        Assert.Equal("Edited content", messages.First().Content);
    }

    [Fact]
    public void MessageDeleted_RemovesMessageFromStore()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = CreateMessage(id: messageId, channelId: TestChannelId);
        SetupMessages(TestChannelId, message);

        // Verify message exists
        Assert.Single(MessageStore.GetMessagesForChannel(TestChannelId));

        // Act - Simulate delete event (ChannelId first, then MessageId)
        SignalR.RaiseMessageDeleted(new MessageDeletedEvent(TestChannelId, messageId));

        // Assert - Message is removed
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        Assert.Empty(messages);
    }

    [Fact]
    public void ReactionUpdated_Added_AddsReactionToMessage()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var reactingUserId = Guid.NewGuid();
        var message = CreateMessage(id: messageId, channelId: TestChannelId);
        SetupMessages(TestChannelId, message);

        var reactionEvent = new ReactionUpdatedEvent(
            MessageId: messageId,
            ChannelId: TestChannelId,
            Emoji: "\U0001F44D",
            Count: 1,
            UserId: reactingUserId,
            Username: "reactor",
            EffectiveDisplayName: "reactor",
            Added: true
        );

        // Act
        SignalR.RaiseReactionUpdated(reactionEvent);

        // Assert
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        var updatedMessage = messages.FirstOrDefault(m => m.Id == messageId);
        Assert.NotNull(updatedMessage);
        Assert.Single(updatedMessage.Reactions);
        Assert.Equal("\U0001F44D", updatedMessage.Reactions.First().Emoji);
    }

    [Fact]
    public void ReactionUpdated_Removed_RemovesReactionFromMessage()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var reactingUserId = Guid.NewGuid();

        // Create message with existing reaction (add reaction first)
        var message = CreateMessage(id: messageId, channelId: TestChannelId);
        SetupMessages(TestChannelId, message);

        // Add a reaction first
        MessageStore.AddReaction(messageId, "\U0001F44D", reactingUserId, "reactor", "reactor");

        // Verify reaction was added
        var beforeMessages = MessageStore.GetMessagesForChannel(TestChannelId);
        Assert.Single(beforeMessages.First().Reactions);

        var reactionEvent = new ReactionUpdatedEvent(
            MessageId: messageId,
            ChannelId: TestChannelId,
            Emoji: "\U0001F44D",
            Count: 0,
            UserId: reactingUserId,
            Username: "reactor",
            EffectiveDisplayName: "reactor",
            Added: false
        );

        // Act
        SignalR.RaiseReactionUpdated(reactionEvent);

        // Assert
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        var updatedMessage = messages.FirstOrDefault(m => m.Id == messageId);
        Assert.NotNull(updatedMessage);
        // Reaction should be removed or have count reduced
        var thumbsUpReaction = updatedMessage.Reactions.FirstOrDefault(r => r.Emoji == "\U0001F44D");
        Assert.True(thumbsUpReaction == null || thumbsUpReaction.Count == 0);
    }

    [Fact]
    public void MessagePinned_UpdatesPinnedState()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = CreateMessage(id: messageId, channelId: TestChannelId);
        SetupMessages(TestChannelId, message);

        // Verify initially not pinned
        var initialMessage = MessageStore.GetMessagesForChannel(TestChannelId).First();
        Assert.False(initialMessage.IsPinned);

        var pinnedEvent = new MessagePinnedEvent(
            MessageId: messageId,
            ChannelId: TestChannelId,
            IsPinned: true,
            PinnedAt: DateTime.UtcNow,
            PinnedByUserId: CurrentUserId,
            PinnedByUsername: "admin",
            PinnedByEffectiveDisplayName: "admin"
        );

        // Act
        SignalR.RaiseMessagePinned(pinnedEvent);

        // Assert
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        var updatedMessage = messages.FirstOrDefault(m => m.Id == messageId);
        Assert.NotNull(updatedMessage);
        Assert.True(updatedMessage.IsPinned);
    }

    [Fact]
    public void MessageUnpinned_UpdatesPinnedState()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = CreateMessage(id: messageId, channelId: TestChannelId);
        SetupMessages(TestChannelId, message);

        // Pin the message first
        MessageStore.UpdatePinState(messageId, true, DateTime.UtcNow, "admin", "admin");

        // Verify pinned
        var pinnedMessage = MessageStore.GetMessagesForChannel(TestChannelId).First();
        Assert.True(pinnedMessage.IsPinned);

        var unpinnedEvent = new MessagePinnedEvent(
            MessageId: messageId,
            ChannelId: TestChannelId,
            IsPinned: false,
            PinnedAt: null,
            PinnedByUserId: CurrentUserId,
            PinnedByUsername: "admin",
            PinnedByEffectiveDisplayName: "admin"
        );

        // Act
        SignalR.RaiseMessagePinned(unpinnedEvent);

        // Assert
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        var updatedMessage = messages.FirstOrDefault(m => m.Id == messageId);
        Assert.NotNull(updatedMessage);
        Assert.False(updatedMessage.IsPinned);
    }

    [Fact]
    public void ThreadMetadataUpdated_UpdatesReplyCount()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = CreateMessage(id: messageId, channelId: TestChannelId);
        SetupMessages(TestChannelId, message);

        var threadEvent = new ThreadMetadataUpdatedEvent(
            ChannelId: TestChannelId,
            MessageId: messageId,
            ReplyCount: 5,
            LastReplyAt: DateTime.UtcNow
        );

        // Act
        SignalR.RaiseThreadMetadataUpdated(threadEvent);

        // Assert
        var messages = MessageStore.GetMessagesForChannel(TestChannelId);
        var updatedMessage = messages.FirstOrDefault(m => m.Id == messageId);
        Assert.NotNull(updatedMessage);
        Assert.Equal(5, updatedMessage.ReplyCount);
    }

    [Fact]
    public void MessageReceived_IncrementsChannelUnreadCount()
    {
        // Arrange
        var channel = CreateChannel(id: TestChannelId);
        SetupChannel(channel);

        // Verify initial unread count
        var initialChannel = ChannelStore.GetChannel(TestChannelId);
        Assert.NotNull(initialChannel);
        Assert.Equal(0, initialChannel.UnreadCount);

        var message = CreateMessage(channelId: TestChannelId);

        // Act
        SignalR.RaiseMessageReceived(message);

        // Assert
        var updatedChannel = ChannelStore.GetChannel(TestChannelId);
        Assert.NotNull(updatedChannel);
        Assert.Equal(1, updatedChannel.UnreadCount);
    }

    [Fact]
    public void MessageReceived_FromCurrentUser_DoesNotIncrementUnreadCount()
    {
        // Arrange
        var channel = CreateChannel(id: TestChannelId);
        SetupChannel(channel);

        // Message from current user (self)
        var message = CreateMessage(
            channelId: TestChannelId,
            authorId: CurrentUserId,
            authorUsername: "me"
        );

        // Act
        SignalR.RaiseMessageReceived(message);

        // Assert - Unread count should NOT increment for own messages
        var updatedChannel = ChannelStore.GetChannel(TestChannelId);
        Assert.NotNull(updatedChannel);
        Assert.Equal(0, updatedChannel.UnreadCount);
    }
}
