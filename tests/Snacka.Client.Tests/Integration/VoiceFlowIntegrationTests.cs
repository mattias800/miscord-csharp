using Snacka.Client.Services;

namespace Snacka.Client.Tests.Integration;

/// <summary>
/// Integration tests for voice-related SignalR event flows.
/// Tests that SignalR events correctly flow through SignalREventDispatcher to update VoiceStore.
/// </summary>
public class VoiceFlowIntegrationTests : ClientIntegrationTestBase
{
    [Fact]
    public void VoiceParticipantJoined_AddsParticipantToStore()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId, username: "Alice");

        var joinedEvent = new VoiceParticipantJoinedEvent(
            ChannelId: TestChannelId,
            Participant: participant
        );

        // Act
        SignalR.RaiseVoiceParticipantJoined(joinedEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        Assert.Single(participants);
        Assert.Equal("Alice", participants.First().Username);
        Assert.Equal(userId, participants.First().UserId);
    }

    [Fact]
    public void VoiceParticipantJoined_MultipleParticipants_AllAddedToStore()
    {
        // Arrange & Act
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        SignalR.RaiseVoiceParticipantJoined(new VoiceParticipantJoinedEvent(
            TestChannelId,
            CreateVoiceParticipant(userId: user1, channelId: TestChannelId, username: "Alice")
        ));
        SignalR.RaiseVoiceParticipantJoined(new VoiceParticipantJoinedEvent(
            TestChannelId,
            CreateVoiceParticipant(userId: user2, channelId: TestChannelId, username: "Bob")
        ));
        SignalR.RaiseVoiceParticipantJoined(new VoiceParticipantJoinedEvent(
            TestChannelId,
            CreateVoiceParticipant(userId: user3, channelId: TestChannelId, username: "Charlie")
        ));

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        Assert.Equal(3, participants.Count);
        Assert.Contains(participants, p => p.Username == "Alice");
        Assert.Contains(participants, p => p.Username == "Bob");
        Assert.Contains(participants, p => p.Username == "Charlie");
    }

    [Fact]
    public void VoiceParticipantLeft_RemovesParticipantFromStore()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId);
        SetupVoiceParticipants(TestChannelId, participant);

        // Verify participant exists
        Assert.Single(VoiceStore.GetParticipantsForChannel(TestChannelId));

        var leftEvent = new VoiceParticipantLeftEvent(
            ChannelId: TestChannelId,
            UserId: userId  // The dispatcher uses userId to find and remove the participant
        );

        // Act
        SignalR.RaiseVoiceParticipantLeft(leftEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        Assert.Empty(participants);
    }

    [Fact]
    public void VoiceStateChanged_UpdatesMutedState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId, isMuted: false);
        SetupVoiceParticipants(TestChannelId, participant);

        var stateUpdate = new VoiceStateUpdate(
            IsMuted: true,
            IsDeafened: null,
            IsScreenSharing: null,
            ScreenShareHasAudio: null,
            IsCameraOn: null
        );

        var stateChangedEvent = new VoiceStateChangedEvent(
            ChannelId: TestChannelId,
            UserId: userId,
            State: stateUpdate
        );

        // Act
        SignalR.RaiseVoiceStateChanged(stateChangedEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        var updatedParticipant = participants.FirstOrDefault(p => p.UserId == userId);
        Assert.NotNull(updatedParticipant);
        Assert.True(updatedParticipant.IsMuted);
    }

    [Fact]
    public void VoiceStateChanged_UpdatesDeafenedState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId, isDeafened: false);
        SetupVoiceParticipants(TestChannelId, participant);

        var stateUpdate = new VoiceStateUpdate(
            IsMuted: null,
            IsDeafened: true,
            IsScreenSharing: null,
            ScreenShareHasAudio: null,
            IsCameraOn: null
        );

        var stateChangedEvent = new VoiceStateChangedEvent(
            ChannelId: TestChannelId,
            UserId: userId,
            State: stateUpdate
        );

        // Act
        SignalR.RaiseVoiceStateChanged(stateChangedEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        var updatedParticipant = participants.FirstOrDefault(p => p.UserId == userId);
        Assert.NotNull(updatedParticipant);
        Assert.True(updatedParticipant.IsDeafened);
    }

    [Fact]
    public void VoiceStateChanged_UpdatesScreenSharingState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId, isScreenSharing: false);
        SetupVoiceParticipants(TestChannelId, participant);

        var stateUpdate = new VoiceStateUpdate(
            IsMuted: null,
            IsDeafened: null,
            IsScreenSharing: true,
            ScreenShareHasAudio: true,
            IsCameraOn: null
        );

        var stateChangedEvent = new VoiceStateChangedEvent(
            ChannelId: TestChannelId,
            UserId: userId,
            State: stateUpdate
        );

        // Act
        SignalR.RaiseVoiceStateChanged(stateChangedEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        var updatedParticipant = participants.FirstOrDefault(p => p.UserId == userId);
        Assert.NotNull(updatedParticipant);
        Assert.True(updatedParticipant.IsScreenSharing);
        Assert.True(updatedParticipant.ScreenShareHasAudio);
    }

    [Fact]
    public void VoiceStateChanged_UpdatesCameraState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId, isCameraOn: false);
        SetupVoiceParticipants(TestChannelId, participant);

        var stateUpdate = new VoiceStateUpdate(
            IsMuted: null,
            IsDeafened: null,
            IsScreenSharing: null,
            ScreenShareHasAudio: null,
            IsCameraOn: true
        );

        var stateChangedEvent = new VoiceStateChangedEvent(
            ChannelId: TestChannelId,
            UserId: userId,
            State: stateUpdate
        );

        // Act
        SignalR.RaiseVoiceStateChanged(stateChangedEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        var updatedParticipant = participants.FirstOrDefault(p => p.UserId == userId);
        Assert.NotNull(updatedParticipant);
        Assert.True(updatedParticipant.IsCameraOn);
    }

    [Fact]
    public void SpeakingStateChanged_UpdatesSpeakingIndicator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId);
        SetupVoiceParticipants(TestChannelId, participant);

        var speakingEvent = new SpeakingStateChangedEvent(
            ChannelId: TestChannelId,
            UserId: userId,
            IsSpeaking: true
        );

        // Act
        SignalR.RaiseSpeakingStateChanged(speakingEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        var updatedParticipant = participants.FirstOrDefault(p => p.UserId == userId);
        Assert.NotNull(updatedParticipant);
        Assert.True(updatedParticipant.IsSpeaking);
    }

    [Fact]
    public void SpeakingStateChanged_StopsSpeaking()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId);
        SetupVoiceParticipants(TestChannelId, participant);

        // First, start speaking
        SignalR.RaiseSpeakingStateChanged(new SpeakingStateChangedEvent(TestChannelId, userId, true));
        Assert.True(VoiceStore.GetParticipantsForChannel(TestChannelId).First().IsSpeaking);

        // Act - Stop speaking
        SignalR.RaiseSpeakingStateChanged(new SpeakingStateChangedEvent(TestChannelId, userId, false));

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        Assert.False(participants.First().IsSpeaking);
    }

    [Fact]
    public void ServerVoiceStateChanged_UpdatesServerMutedState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId);
        SetupVoiceParticipants(TestChannelId, participant);

        var serverStateEvent = new ServerVoiceStateChangedEvent(
            ChannelId: TestChannelId,
            TargetUserId: userId,
            AdminUserId: Guid.NewGuid(),
            AdminUsername: "admin",
            IsServerMuted: true,
            IsServerDeafened: null
        );

        // Act
        SignalR.RaiseServerVoiceStateChanged(serverStateEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        var updatedParticipant = participants.FirstOrDefault(p => p.UserId == userId);
        Assert.NotNull(updatedParticipant);
        Assert.True(updatedParticipant.IsServerMuted);
    }

    [Fact]
    public void ServerVoiceStateChanged_UpdatesServerDeafenedState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = CreateVoiceParticipant(userId: userId, channelId: TestChannelId);
        SetupVoiceParticipants(TestChannelId, participant);

        var serverStateEvent = new ServerVoiceStateChangedEvent(
            ChannelId: TestChannelId,
            TargetUserId: userId,
            AdminUserId: Guid.NewGuid(),
            AdminUsername: "admin",
            IsServerMuted: null,
            IsServerDeafened: true
        );

        // Act
        SignalR.RaiseServerVoiceStateChanged(serverStateEvent);

        // Assert
        var participants = VoiceStore.GetParticipantsForChannel(TestChannelId);
        var updatedParticipant = participants.FirstOrDefault(p => p.UserId == userId);
        Assert.NotNull(updatedParticipant);
        Assert.True(updatedParticipant.IsServerDeafened);
    }

    [Fact]
    public void VoiceParticipants_DifferentChannels_AreIsolated()
    {
        // Arrange
        var channel1 = Guid.NewGuid();
        var channel2 = Guid.NewGuid();

        // Act
        SignalR.RaiseVoiceParticipantJoined(new VoiceParticipantJoinedEvent(
            channel1,
            CreateVoiceParticipant(channelId: channel1, username: "Alice")
        ));
        SignalR.RaiseVoiceParticipantJoined(new VoiceParticipantJoinedEvent(
            channel2,
            CreateVoiceParticipant(channelId: channel2, username: "Bob")
        ));

        // Assert - Each channel has only its participant
        var channel1Participants = VoiceStore.GetParticipantsForChannel(channel1);
        var channel2Participants = VoiceStore.GetParticipantsForChannel(channel2);

        Assert.Single(channel1Participants);
        Assert.Equal("Alice", channel1Participants.First().Username);

        Assert.Single(channel2Participants);
        Assert.Equal("Bob", channel2Participants.First().Username);
    }
}
