using Snacka.Client.Services;
using Snacka.Client.Stores;
using Snacka.Shared.Models;

namespace Snacka.Client.Tests.Integration;

/// <summary>
/// Base class for client integration tests.
/// Wires up real stores with a mock SignalR service, allowing tests to verify
/// that SignalR events flow correctly through the system to update stores.
/// </summary>
public class ClientIntegrationTestBase : IDisposable
{
    // The mock SignalR service - tests use this to raise events
    protected readonly MockSignalRService SignalR;

    // Real stores - tests verify state changes here
    protected readonly IPresenceStore PresenceStore;
    protected readonly IChannelStore ChannelStore;
    protected readonly IMessageStore MessageStore;
    protected readonly ICommunityStore CommunityStore;
    protected readonly IVoiceStore VoiceStore;
    protected readonly IGamingStationStore GamingStationStore;
    protected readonly ITypingStore TypingStore;

    // Store container for convenience
    protected readonly StoreContainer Stores;

    // The dispatcher that routes SignalR events to stores
    protected readonly ISignalREventDispatcher Dispatcher;

    // Test user ID - represents the "current user" in tests
    protected readonly Guid CurrentUserId = Guid.NewGuid();

    // Common test data
    protected readonly Guid TestCommunityId = Guid.NewGuid();
    protected readonly Guid TestChannelId = Guid.NewGuid();

    public ClientIntegrationTestBase()
    {
        // Create mock SignalR service
        SignalR = new MockSignalRService();

        // Create real stores
        PresenceStore = new PresenceStore();
        ChannelStore = new ChannelStore();
        MessageStore = new MessageStore();
        CommunityStore = new CommunityStore();
        VoiceStore = new VoiceStore();
        GamingStationStore = new GamingStationStore();
        TypingStore = new TypingStore();

        Stores = new StoreContainer(
            PresenceStore,
            ChannelStore,
            MessageStore,
            CommunityStore,
            VoiceStore,
            GamingStationStore,
            TypingStore
        );

        // Create and initialize the dispatcher
        Dispatcher = new SignalREventDispatcher(SignalR);
        Dispatcher.Initialize(
            ChannelStore,
            CommunityStore,
            MessageStore,
            VoiceStore,
            PresenceStore,
            GamingStationStore,
            TypingStore,
            CurrentUserId
        );
    }

    #region Test Data Factories

    /// <summary>
    /// Creates a test channel response.
    /// </summary>
    protected ChannelResponse CreateChannel(
        Guid? id = null,
        string name = "test-channel",
        ChannelType type = ChannelType.Text,
        Guid? communityId = null,
        int position = 0)
    {
        return new ChannelResponse(
            Id: id ?? Guid.NewGuid(),
            Name: name,
            Topic: null,
            CommunityId: communityId ?? TestCommunityId,
            Type: type,
            Position: position,
            CreatedAt: DateTime.UtcNow,
            UnreadCount: 0
        );
    }

    /// <summary>
    /// Creates a test message response.
    /// </summary>
    protected MessageResponse CreateMessage(
        Guid? id = null,
        Guid? channelId = null,
        Guid? authorId = null,
        string content = "Test message",
        string authorUsername = "testuser")
    {
        return new MessageResponse(
            Id: id ?? Guid.NewGuid(),
            Content: content,
            AuthorId: authorId ?? Guid.NewGuid(),
            AuthorUsername: authorUsername,
            AuthorEffectiveDisplayName: authorUsername,
            AuthorAvatar: null,
            ChannelId: channelId ?? TestChannelId,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            IsEdited: false
        );
    }

    /// <summary>
    /// Creates a test voice participant response.
    /// </summary>
    protected VoiceParticipantResponse CreateVoiceParticipant(
        Guid? userId = null,
        Guid? channelId = null,
        string username = "testuser",
        bool isMuted = false,
        bool isDeafened = false,
        bool isScreenSharing = false,
        bool isCameraOn = false)
    {
        return new VoiceParticipantResponse(
            Id: Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            Username: username,
            ChannelId: channelId ?? TestChannelId,
            IsMuted: isMuted,
            IsDeafened: isDeafened,
            IsServerMuted: false,
            IsServerDeafened: false,
            IsScreenSharing: isScreenSharing,
            ScreenShareHasAudio: false,
            IsCameraOn: isCameraOn,
            JoinedAt: DateTime.UtcNow,
            IsGamingStation: false,
            GamingStationMachineId: null
        );
    }

    /// <summary>
    /// Creates a test user presence event.
    /// </summary>
    protected UserPresenceEvent CreatePresenceEvent(
        Guid? userId = null,
        string username = "testuser",
        bool isOnline = true)
    {
        return new UserPresenceEvent(
            UserId: userId ?? Guid.NewGuid(),
            Username: username,
            IsOnline: isOnline
        );
    }

    /// <summary>
    /// Creates a test typing event.
    /// </summary>
    protected TypingEvent CreateTypingEvent(
        Guid? userId = null,
        Guid? channelId = null,
        string username = "testuser")
    {
        return new TypingEvent(
            UserId: userId ?? Guid.NewGuid(),
            ChannelId: channelId ?? TestChannelId,
            Username: username
        );
    }

    #endregion

    #region Setup Helpers

    /// <summary>
    /// Sets up a channel in the store before testing.
    /// </summary>
    protected void SetupChannel(ChannelResponse channel)
    {
        ChannelStore.SetChannels(new[] { channel });
    }

    /// <summary>
    /// Sets up messages for a channel before testing.
    /// </summary>
    protected void SetupMessages(Guid channelId, params MessageResponse[] messages)
    {
        MessageStore.SetMessages(channelId, messages);
    }

    /// <summary>
    /// Sets up voice participants for a channel before testing.
    /// </summary>
    protected void SetupVoiceParticipants(Guid channelId, params VoiceParticipantResponse[] participants)
    {
        VoiceStore.SetParticipants(channelId, participants);
    }

    #endregion

    public virtual void Dispose()
    {
        Dispatcher.Dispose();
        (PresenceStore as IDisposable)?.Dispose();
        (ChannelStore as IDisposable)?.Dispose();
        (MessageStore as IDisposable)?.Dispose();
        (CommunityStore as IDisposable)?.Dispose();
        (VoiceStore as IDisposable)?.Dispose();
        (GamingStationStore as IDisposable)?.Dispose();
        (TypingStore as IDisposable)?.Dispose();
    }
}
