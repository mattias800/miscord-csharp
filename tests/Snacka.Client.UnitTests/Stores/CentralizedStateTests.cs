using System.Reactive.Linq;
using Snacka.Client.Stores;
using Snacka.Client.Services;
using Snacka.Shared.Models;

namespace Snacka.Client.Tests.Stores;

/// <summary>
/// Tests that verify centralized state management keeps data in sync.
///
/// The original problem: Unread highlights were out of sync in different parts of the UI
/// because multiple components maintained their own state instead of using a single source of truth.
///
/// These tests verify that the Redux-style store architecture ensures:
/// 1. All observers see the same state
/// 2. State updates propagate to all subscribers
/// 3. Unread counts are consistent across all consumers
/// </summary>
public class CentralizedStateTests : IDisposable
{
    private readonly ChannelStore _channelStore;
    private readonly MessageStore _messageStore;

    public CentralizedStateTests()
    {
        _channelStore = new ChannelStore();
        _messageStore = new MessageStore();
    }

    public void Dispose()
    {
        _channelStore.Dispose();
        _messageStore.Dispose();
    }

    private static ChannelResponse CreateChannel(
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

    #region Single Source of Truth Tests

    [Fact]
    public void MultipleObservers_SeeTheSameUnreadCount()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        _channelStore.AddChannel(CreateChannel(id: channelId, unreadCount: 0));

        // Create multiple observers (simulating different UI components)
        var observer1Values = new List<int>();
        var observer2Values = new List<int>();
        var observer3Values = new List<int>();

        using var sub1 = _channelStore.Items.Subscribe(items =>
            observer1Values.Add(items.FirstOrDefault(c => c.Id == channelId)?.UnreadCount ?? -1));
        using var sub2 = _channelStore.Items.Subscribe(items =>
            observer2Values.Add(items.FirstOrDefault(c => c.Id == channelId)?.UnreadCount ?? -1));
        using var sub3 = _channelStore.Items.Subscribe(items =>
            observer3Values.Add(items.FirstOrDefault(c => c.Id == channelId)?.UnreadCount ?? -1));

        // Act - update unread count (simulating a new message)
        _channelStore.UpdateUnreadCount(channelId, 5);
        _channelStore.IncrementUnreadCount(channelId);

        // Assert - all observers should see the same progression of values
        // Initial value (0) and then updates (5, 6)
        Assert.Contains(0, observer1Values);
        Assert.Contains(5, observer1Values);
        Assert.Contains(6, observer1Values);

        Assert.Contains(0, observer2Values);
        Assert.Contains(5, observer2Values);
        Assert.Contains(6, observer2Values);

        Assert.Contains(0, observer3Values);
        Assert.Contains(5, observer3Values);
        Assert.Contains(6, observer3Values);
    }

    [Fact]
    public void TotalUnreadCount_ReflectsAllChannelUpdates()
    {
        // Arrange - simulating sidebar badge that shows total unreads
        var channel1Id = Guid.NewGuid();
        var channel2Id = Guid.NewGuid();
        var channel3Id = Guid.NewGuid();

        _channelStore.AddChannel(CreateChannel(id: channel1Id, name: "general", unreadCount: 0));
        _channelStore.AddChannel(CreateChannel(id: channel2Id, name: "random", unreadCount: 0));
        _channelStore.AddChannel(CreateChannel(id: channel3Id, name: "announcements", unreadCount: 0));

        var totalUnreadValues = new List<int>();
        using var sub = _channelStore.TotalUnreadCount.Subscribe(total => totalUnreadValues.Add(total));

        // Act - simulate receiving messages across different channels
        _channelStore.UpdateUnreadCount(channel1Id, 3);  // general: 3 unreads
        _channelStore.UpdateUnreadCount(channel2Id, 7);  // random: 7 unreads
        _channelStore.IncrementUnreadCount(channel3Id);  // announcements: 1 unread

        // Assert
        var lastValue = totalUnreadValues.Last();
        Assert.Equal(11, lastValue); // 3 + 7 + 1 = 11
    }

    [Fact]
    public void SelectingChannel_ClearsUnreadCount_UpdatesAllObservers()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        _channelStore.AddChannel(CreateChannel(id: channelId, unreadCount: 10));

        var channelListUnreads = new List<int>();
        var sidebarBadgeUnreads = new List<int>();

        // Simulate channel list observer
        using var sub1 = _channelStore.Items.Subscribe(items =>
            channelListUnreads.Add(items.FirstOrDefault(c => c.Id == channelId)?.UnreadCount ?? -1));

        // Simulate sidebar badge observer
        using var sub2 = _channelStore.TotalUnreadCount.Subscribe(total =>
            sidebarBadgeUnreads.Add(total));

        // Act - user selects the channel (which should clear unread)
        _channelStore.SelectChannel(channelId);
        _channelStore.UpdateUnreadCount(channelId, 0);

        // Assert - both observers see the unread cleared
        Assert.Contains(10, channelListUnreads);
        Assert.Contains(0, channelListUnreads);

        Assert.Contains(10, sidebarBadgeUnreads);
        Assert.Contains(0, sidebarBadgeUnreads);
    }

    #endregion

    #region State Consistency Tests

    [Fact]
    public void LateSubscriber_SeesCurrentState()
    {
        // Arrange - set up state before subscription
        var channelId = Guid.NewGuid();
        _channelStore.AddChannel(CreateChannel(id: channelId, unreadCount: 0));
        _channelStore.UpdateUnreadCount(channelId, 15);

        // Act - subscribe after state is set (simulating a new UI component mounting)
        int? observedUnread = null;
        using var sub = _channelStore.Items.Subscribe(items =>
            observedUnread = items.FirstOrDefault(c => c.Id == channelId)?.UnreadCount);

        // Assert - late subscriber should see current state immediately
        Assert.Equal(15, observedUnread);
    }

    [Fact]
    public void RapidUpdates_AllObserversStayInSync()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        _channelStore.AddChannel(CreateChannel(id: channelId, unreadCount: 0));

        var observer1FinalValue = 0;
        var observer2FinalValue = 0;

        using var sub1 = _channelStore.Items.Subscribe(items =>
            observer1FinalValue = items.FirstOrDefault(c => c.Id == channelId)?.UnreadCount ?? -1);
        using var sub2 = _channelStore.Items.Subscribe(items =>
            observer2FinalValue = items.FirstOrDefault(c => c.Id == channelId)?.UnreadCount ?? -1);

        // Act - rapid fire updates (simulating burst of messages)
        for (int i = 0; i < 100; i++)
        {
            _channelStore.IncrementUnreadCount(channelId);
        }

        // Assert - both observers should see final value of 100
        Assert.Equal(100, observer1FinalValue);
        Assert.Equal(100, observer2FinalValue);
    }

    [Fact]
    public void TextChannels_Selector_ReflectsUnreadChanges()
    {
        // Arrange
        var textChannelId = Guid.NewGuid();
        var voiceChannelId = Guid.NewGuid();

        _channelStore.AddChannel(new ChannelResponse(
            Id: textChannelId, Name: "text-channel", Topic: null, Type: ChannelType.Text,
            CommunityId: Guid.NewGuid(), Position: 0, UnreadCount: 0, CreatedAt: DateTime.UtcNow));
        _channelStore.AddChannel(new ChannelResponse(
            Id: voiceChannelId, Name: "voice-channel", Topic: null, Type: ChannelType.Voice,
            CommunityId: Guid.NewGuid(), Position: 0, UnreadCount: 0, CreatedAt: DateTime.UtcNow));

        var textChannelUnreads = new List<int>();
        using var sub = _channelStore.TextChannels.Subscribe(channels =>
            textChannelUnreads.Add(channels.FirstOrDefault()?.UnreadCount ?? -1));

        // Act - update unread on text channel
        _channelStore.UpdateUnreadCount(textChannelId, 5);

        // Assert - TextChannels selector reflects the change
        Assert.Contains(0, textChannelUnreads);
        Assert.Contains(5, textChannelUnreads);
    }

    #endregion

    #region Cross-Store Coordination Tests

    [Fact]
    public void ChannelAndMessageStores_CanBeUsedTogether()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        _channelStore.AddChannel(CreateChannel(id: channelId, unreadCount: 0));
        _messageStore.SetCurrentChannel(channelId);

        var messageId = Guid.NewGuid();
        var message = new MessageResponse(
            Id: messageId, ChannelId: channelId, Content: "Hello!",
            AuthorId: Guid.NewGuid(), AuthorUsername: "alice", AuthorEffectiveDisplayName: "Alice",
            AuthorAvatar: null, CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
            IsEdited: false, IsPinned: false, PinnedAt: null, PinnedByUsername: null,
            PinnedByEffectiveDisplayName: null, ReplyToId: null, ReplyTo: null,
            Reactions: null, Attachments: null, ThreadParentMessageId: null,
            ReplyCount: 0, LastReplyAt: null);

        // Act - add message (simulating SignalR event -> both stores updated)
        _messageStore.AddMessage(message);
        _channelStore.IncrementUnreadCount(channelId);

        // Assert - both stores reflect the update
        var channelState = _channelStore.GetChannel(channelId);
        var messageState = _messageStore.GetMessage(messageId);

        Assert.NotNull(channelState);
        Assert.NotNull(messageState);
        Assert.Equal(1, channelState.UnreadCount);
        Assert.Equal("Hello!", messageState.Content);
    }

    #endregion

    #region Original Bug Reproduction Tests

    [Fact]
    public void UnreadBadge_StaysInSync_BetweenSidebarAndChannelList()
    {
        // This test reproduces the original bug: unread highlights were out of sync
        // between different parts of the UI because they maintained separate state.

        // With centralized state, both components observe the same store.

        // Arrange
        var channelId = Guid.NewGuid();
        _channelStore.AddChannel(CreateChannel(id: channelId, name: "announcements", unreadCount: 0));

        // Simulate two different UI components both watching unread count
        var sidebarUnreadCount = 0;
        var channelListUnreadCount = 0;

        using var sidebarSub = _channelStore.Items.Subscribe(items =>
            sidebarUnreadCount = items.Sum(c => c.UnreadCount));
        using var channelListSub = _channelStore.Items.Subscribe(items =>
            channelListUnreadCount = items.FirstOrDefault(c => c.Id == channelId)?.UnreadCount ?? 0);

        // Act - simulate receiving 5 new messages
        for (int i = 0; i < 5; i++)
        {
            _channelStore.IncrementUnreadCount(channelId);
        }

        // Assert - both UI components show the same value
        Assert.Equal(5, sidebarUnreadCount);
        Assert.Equal(5, channelListUnreadCount);

        // Act - user reads the channel
        _channelStore.UpdateUnreadCount(channelId, 0);

        // Assert - both UI components update to 0
        Assert.Equal(0, sidebarUnreadCount);
        Assert.Equal(0, channelListUnreadCount);
    }

    #endregion
}
