using Snacka.Client.Services;
using Snacka.Client.ViewModels;
using Snacka.Shared.Models;

namespace Snacka.Client.Tests.ViewModels;

public class VoiceChannelViewModelTests : IDisposable
{
    private readonly Guid _currentUserId;
    private readonly Guid _channelId;
    private readonly ChannelResponse _channel;

    public VoiceChannelViewModelTests()
    {
        _currentUserId = Guid.NewGuid();
        _channelId = Guid.NewGuid();
        _channel = CreateChannelResponse(_channelId);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private static ChannelResponse CreateChannelResponse(
        Guid? id = null,
        string name = "Voice Channel",
        int position = 0)
    {
        return new ChannelResponse(
            Id: id ?? Guid.NewGuid(),
            Name: name,
            Topic: null,
            CommunityId: Guid.NewGuid(),
            Type: ChannelType.Voice,
            Position: position,
            CreatedAt: DateTime.UtcNow,
            UnreadCount: 0
        );
    }

    private static VoiceParticipantResponse CreateParticipantResponse(
        Guid? userId = null,
        Guid? channelId = null,
        string username = "testuser",
        bool isMuted = false,
        bool isDeafened = false,
        bool isServerMuted = false,
        bool isServerDeafened = false,
        bool isScreenSharing = false,
        bool isCameraOn = false)
    {
        return new VoiceParticipantResponse(
            Id: Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            Username: username,
            ChannelId: channelId ?? Guid.NewGuid(),
            IsMuted: isMuted,
            IsDeafened: isDeafened,
            IsServerMuted: isServerMuted,
            IsServerDeafened: isServerDeafened,
            IsScreenSharing: isScreenSharing,
            ScreenShareHasAudio: false,
            IsCameraOn: isCameraOn,
            JoinedAt: DateTime.UtcNow,
            IsGamingStation: false,
            GamingStationMachineId: null
        );
    }

    #region VoiceParticipantViewModel Tests

    [Fact]
    public void VoiceParticipantViewModel_Constructor_SetsInitialState()
    {
        // Arrange
        var participant = CreateParticipantResponse(username: "Alice");

        // Act
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Assert
        Assert.Equal("Alice", vm.Username);
        Assert.Equal(participant.UserId, vm.UserId);
        Assert.False(vm.IsSpeaking);
        Assert.Equal(1.0f, vm.Volume);
        Assert.Equal(100, vm.VolumePercent);
    }

    [Fact]
    public void VoiceParticipantViewModel_IsCurrentUser_ReturnsTrueForCurrentUser()
    {
        // Arrange
        var participant = CreateParticipantResponse(userId: _currentUserId);

        // Act
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Assert
        Assert.True(vm.IsCurrentUser);
    }

    [Fact]
    public void VoiceParticipantViewModel_IsCurrentUser_ReturnsFalseForOtherUser()
    {
        // Arrange
        var participant = CreateParticipantResponse(userId: Guid.NewGuid());

        // Act
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Assert
        Assert.False(vm.IsCurrentUser);
    }

    [Fact]
    public void VoiceParticipantViewModel_IsSpeaking_RaisesPropertyChanged()
    {
        // Arrange
        var participant = CreateParticipantResponse();
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);
        var propertyChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VoiceParticipantViewModel.IsSpeaking))
                propertyChanged = true;
        };

        // Act
        vm.IsSpeaking = true;

        // Assert
        Assert.True(propertyChanged);
        Assert.True(vm.IsSpeaking);
    }

    [Fact]
    public void VoiceParticipantViewModel_Volume_ClampsTo0To2()
    {
        // Arrange
        var participant = CreateParticipantResponse();
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Act & Assert - Below 0
        vm.Volume = -1f;
        Assert.Equal(0f, vm.Volume);

        // Act & Assert - Above 2
        vm.Volume = 3f;
        Assert.Equal(2f, vm.Volume);

        // Act & Assert - Within range
        vm.Volume = 1.5f;
        Assert.Equal(1.5f, vm.Volume);
    }

    [Fact]
    public void VoiceParticipantViewModel_Volume_CallsOnVolumeChanged()
    {
        // Arrange
        var participant = CreateParticipantResponse();
        float? changedVolume = null;
        Guid? changedUserId = null;

        var vm = new VoiceParticipantViewModel(
            participant,
            _currentUserId,
            1.0f,
            (userId, volume) =>
            {
                changedUserId = userId;
                changedVolume = volume;
            });

        // Act
        vm.Volume = 0.5f;

        // Assert
        Assert.Equal(participant.UserId, changedUserId);
        Assert.Equal(0.5f, changedVolume);
    }

    [Fact]
    public void VoiceParticipantViewModel_VolumePercent_ConvertsCorrectly()
    {
        // Arrange
        var participant = CreateParticipantResponse();
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Act
        vm.VolumePercent = 150;

        // Assert
        Assert.Equal(1.5f, vm.Volume);
        Assert.Equal(150, vm.VolumePercent);
    }

    [Fact]
    public void VoiceParticipantViewModel_IsEffectivelyMuted_TrueWhenSelfMuted()
    {
        // Arrange
        var participant = CreateParticipantResponse(isMuted: true, isServerMuted: false);

        // Act
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Assert
        Assert.True(vm.IsEffectivelyMuted);
    }

    [Fact]
    public void VoiceParticipantViewModel_IsEffectivelyMuted_TrueWhenServerMuted()
    {
        // Arrange
        var participant = CreateParticipantResponse(isMuted: false, isServerMuted: true);

        // Act
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Assert
        Assert.True(vm.IsEffectivelyMuted);
    }

    [Fact]
    public void VoiceParticipantViewModel_IsEffectivelyMuted_FalseWhenNeither()
    {
        // Arrange
        var participant = CreateParticipantResponse(isMuted: false, isServerMuted: false);

        // Act
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Assert
        Assert.False(vm.IsEffectivelyMuted);
    }

    [Fact]
    public void VoiceParticipantViewModel_IsEffectivelyDeafened_TrueWhenSelfDeafened()
    {
        // Arrange
        var participant = CreateParticipantResponse(isDeafened: true, isServerDeafened: false);

        // Act
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Assert
        Assert.True(vm.IsEffectivelyDeafened);
    }

    [Fact]
    public void VoiceParticipantViewModel_UpdateState_UpdatesParticipant()
    {
        // Arrange
        var participant = CreateParticipantResponse(isMuted: false, isScreenSharing: false);
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);
        var propertyChangedNames = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                propertyChangedNames.Add(e.PropertyName);
        };

        // Act
        vm.UpdateState(new VoiceStateUpdate(IsMuted: true, IsScreenSharing: true));

        // Assert
        Assert.True(vm.IsMuted);
        Assert.True(vm.IsScreenSharing);
        Assert.Contains(nameof(VoiceParticipantViewModel.IsMuted), propertyChangedNames);
        Assert.Contains(nameof(VoiceParticipantViewModel.IsScreenSharing), propertyChangedNames);
    }

    [Fact]
    public void VoiceParticipantViewModel_UpdateState_NullValuesPreserveExisting()
    {
        // Arrange
        var participant = CreateParticipantResponse(isMuted: true, isCameraOn: true);
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);

        // Act - only update screen sharing, not muted or camera
        vm.UpdateState(new VoiceStateUpdate(IsScreenSharing: true));

        // Assert - muted and camera should be unchanged
        Assert.True(vm.IsMuted);
        Assert.True(vm.IsCameraOn);
        Assert.True(vm.IsScreenSharing);
    }

    [Fact]
    public void VoiceParticipantViewModel_UpdateServerState_UpdatesServerMuteDeafen()
    {
        // Arrange
        var participant = CreateParticipantResponse(isServerMuted: false, isServerDeafened: false);
        var vm = new VoiceParticipantViewModel(participant, _currentUserId);
        var propertyChangedNames = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                propertyChangedNames.Add(e.PropertyName);
        };

        // Act
        vm.UpdateServerState(isServerMuted: true, isServerDeafened: true);

        // Assert
        Assert.True(vm.IsServerMuted);
        Assert.True(vm.IsServerDeafened);
        Assert.True(vm.IsEffectivelyMuted);
        Assert.True(vm.IsEffectivelyDeafened);
        Assert.Contains(nameof(VoiceParticipantViewModel.IsServerMuted), propertyChangedNames);
        Assert.Contains(nameof(VoiceParticipantViewModel.IsServerDeafened), propertyChangedNames);
    }

    #endregion

    #region VoiceChannelViewModel Constructor Tests

    [Fact]
    public void VoiceChannelViewModel_Constructor_SetsChannelProperties()
    {
        // Act
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);

        // Assert
        Assert.Equal(_channelId, vm.Id);
        Assert.Equal("Voice Channel", vm.Name);
        Assert.Equal(_channel, vm.Channel);
        Assert.Empty(vm.Participants);
    }

    [Fact]
    public void VoiceChannelViewModel_Constructor_SetsPositionFromChannel()
    {
        // Arrange
        var channel = CreateChannelResponse(position: 5);

        // Act
        var vm = new VoiceChannelViewModel(channel, _currentUserId);

        // Assert
        Assert.Equal(5, vm.Position);
    }

    [Fact]
    public void VoiceChannelViewModel_Position_CanBeChanged()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var propertyChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VoiceChannelViewModel.Position))
                propertyChanged = true;
        };

        // Act
        vm.Position = 10;

        // Assert
        Assert.Equal(10, vm.Position);
        Assert.True(propertyChanged);
    }

    #endregion

    #region VoiceChannelViewModel AddParticipant Tests

    [Fact]
    public void VoiceChannelViewModel_AddParticipant_AddsNewParticipant()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant = CreateParticipantResponse(channelId: _channelId, username: "Alice");

        // Act
        vm.AddParticipant(participant);

        // Assert
        Assert.Single(vm.Participants);
        Assert.Equal("Alice", vm.Participants[0].Username);
    }

    [Fact]
    public void VoiceChannelViewModel_AddParticipant_DoesNotAddDuplicate()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var userId = Guid.NewGuid();
        var participant1 = CreateParticipantResponse(userId: userId, username: "Alice");
        var participant2 = CreateParticipantResponse(userId: userId, username: "Alice Updated");

        // Act
        vm.AddParticipant(participant1);
        vm.AddParticipant(participant2);

        // Assert
        Assert.Single(vm.Participants);
        Assert.Equal("Alice", vm.Participants[0].Username); // Original name kept
    }

    [Fact]
    public void VoiceChannelViewModel_AddParticipant_UsesGetInitialVolume()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var vm = new VoiceChannelViewModel(
            _channel,
            _currentUserId,
            onVolumeChanged: null,
            getInitialVolume: uid => uid == userId ? 0.5f : 1.0f);
        var participant = CreateParticipantResponse(userId: userId);

        // Act
        vm.AddParticipant(participant);

        // Assert
        Assert.Equal(0.5f, vm.Participants[0].Volume);
    }

    [Fact]
    public void VoiceChannelViewModel_AddParticipant_PropagatesVolumeCallback()
    {
        // Arrange
        float? changedVolume = null;
        Guid? changedUserId = null;
        var vm = new VoiceChannelViewModel(
            _channel,
            _currentUserId,
            onVolumeChanged: (uid, vol) =>
            {
                changedUserId = uid;
                changedVolume = vol;
            });
        var participant = CreateParticipantResponse();
        vm.AddParticipant(participant);

        // Act
        vm.Participants[0].Volume = 0.75f;

        // Assert
        Assert.Equal(participant.UserId, changedUserId);
        Assert.Equal(0.75f, changedVolume);
    }

    #endregion

    #region VoiceChannelViewModel RemoveParticipant Tests

    [Fact]
    public void VoiceChannelViewModel_RemoveParticipant_RemovesExisting()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant = CreateParticipantResponse();
        vm.AddParticipant(participant);
        Assert.Single(vm.Participants);

        // Act
        vm.RemoveParticipant(participant.UserId);

        // Assert
        Assert.Empty(vm.Participants);
    }

    [Fact]
    public void VoiceChannelViewModel_RemoveParticipant_DoesNothingForNonexistent()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant = CreateParticipantResponse();
        vm.AddParticipant(participant);

        // Act - try to remove a different user
        vm.RemoveParticipant(Guid.NewGuid());

        // Assert - original participant still there
        Assert.Single(vm.Participants);
    }

    [Fact]
    public void VoiceChannelViewModel_RemoveParticipant_RemovesCorrectOne()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant1 = CreateParticipantResponse(username: "Alice");
        var participant2 = CreateParticipantResponse(username: "Bob");
        vm.AddParticipant(participant1);
        vm.AddParticipant(participant2);
        Assert.Equal(2, vm.Participants.Count);

        // Act
        vm.RemoveParticipant(participant1.UserId);

        // Assert
        Assert.Single(vm.Participants);
        Assert.Equal("Bob", vm.Participants[0].Username);
    }

    #endregion

    #region VoiceChannelViewModel UpdateParticipantState Tests

    [Fact]
    public void VoiceChannelViewModel_UpdateParticipantState_UpdatesExisting()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant = CreateParticipantResponse(isMuted: false);
        vm.AddParticipant(participant);
        Assert.False(vm.Participants[0].IsMuted);

        // Act
        vm.UpdateParticipantState(participant.UserId, new VoiceStateUpdate(IsMuted: true));

        // Assert
        Assert.True(vm.Participants[0].IsMuted);
    }

    [Fact]
    public void VoiceChannelViewModel_UpdateParticipantState_DoesNothingForNonexistent()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant = CreateParticipantResponse();
        vm.AddParticipant(participant);

        // Act - should not throw for nonexistent user
        vm.UpdateParticipantState(Guid.NewGuid(), new VoiceStateUpdate(IsMuted: true));

        // Assert - original participant unchanged
        Assert.False(vm.Participants[0].IsMuted);
    }

    #endregion

    #region VoiceChannelViewModel UpdateSpeakingState Tests

    [Fact]
    public void VoiceChannelViewModel_UpdateSpeakingState_UpdatesParticipant()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant = CreateParticipantResponse();
        vm.AddParticipant(participant);
        Assert.False(vm.Participants[0].IsSpeaking);

        // Act
        vm.UpdateSpeakingState(participant.UserId, true);

        // Assert
        Assert.True(vm.Participants[0].IsSpeaking);
    }

    [Fact]
    public void VoiceChannelViewModel_UpdateSpeakingState_DoesNothingForNonexistent()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant = CreateParticipantResponse();
        vm.AddParticipant(participant);

        // Act - should not throw
        vm.UpdateSpeakingState(Guid.NewGuid(), true);

        // Assert - original participant unchanged
        Assert.False(vm.Participants[0].IsSpeaking);
    }

    #endregion

    #region VoiceChannelViewModel UpdateServerState Tests

    [Fact]
    public void VoiceChannelViewModel_UpdateServerState_UpdatesParticipant()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var participant = CreateParticipantResponse(isServerMuted: false);
        vm.AddParticipant(participant);

        // Act
        vm.UpdateServerState(participant.UserId, isServerMuted: true, isServerDeafened: false);

        // Assert
        Assert.True(vm.Participants[0].IsServerMuted);
        Assert.False(vm.Participants[0].IsServerDeafened);
    }

    #endregion

    #region VoiceChannelViewModel SetParticipants Tests

    [Fact]
    public void VoiceChannelViewModel_SetParticipants_ReplacesAll()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var existingParticipant = CreateParticipantResponse(username: "Existing");
        vm.AddParticipant(existingParticipant);
        Assert.Single(vm.Participants);

        var newParticipants = new List<VoiceParticipantResponse>
        {
            CreateParticipantResponse(username: "New1"),
            CreateParticipantResponse(username: "New2"),
            CreateParticipantResponse(username: "New3")
        };

        // Act
        vm.SetParticipants(newParticipants);

        // Assert
        Assert.Equal(3, vm.Participants.Count);
        Assert.Equal("New1", vm.Participants[0].Username);
        Assert.Equal("New2", vm.Participants[1].Username);
        Assert.Equal("New3", vm.Participants[2].Username);
    }

    [Fact]
    public void VoiceChannelViewModel_SetParticipants_ClearsWhenEmpty()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        vm.AddParticipant(CreateParticipantResponse());
        vm.AddParticipant(CreateParticipantResponse());
        Assert.Equal(2, vm.Participants.Count);

        // Act
        vm.SetParticipants(new List<VoiceParticipantResponse>());

        // Assert
        Assert.Empty(vm.Participants);
    }

    [Fact]
    public void VoiceChannelViewModel_SetParticipants_UsesGetInitialVolume()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var vm = new VoiceChannelViewModel(
            _channel,
            _currentUserId,
            onVolumeChanged: null,
            getInitialVolume: uid =>
            {
                if (uid == userId1) return 0.3f;
                if (uid == userId2) return 0.7f;
                return 1.0f;
            });

        var participants = new List<VoiceParticipantResponse>
        {
            CreateParticipantResponse(userId: userId1, username: "User1"),
            CreateParticipantResponse(userId: userId2, username: "User2")
        };

        // Act
        vm.SetParticipants(participants);

        // Assert
        var user1Vm = vm.Participants.First(p => p.UserId == userId1);
        var user2Vm = vm.Participants.First(p => p.UserId == userId2);
        Assert.Equal(0.3f, user1Vm.Volume);
        Assert.Equal(0.7f, user2Vm.Volume);
    }

    #endregion

    #region VoiceChannelViewModel Drag Preview State Tests

    [Fact]
    public void VoiceChannelViewModel_ShowGapAbove_RaisesPropertyChanged()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var propertyChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VoiceChannelViewModel.ShowGapAbove))
                propertyChanged = true;
        };

        // Act
        vm.ShowGapAbove = true;

        // Assert
        Assert.True(propertyChanged);
        Assert.True(vm.ShowGapAbove);
    }

    [Fact]
    public void VoiceChannelViewModel_ShowGapBelow_RaisesPropertyChanged()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var propertyChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VoiceChannelViewModel.ShowGapBelow))
                propertyChanged = true;
        };

        // Act
        vm.ShowGapBelow = true;

        // Assert
        Assert.True(propertyChanged);
        Assert.True(vm.ShowGapBelow);
    }

    [Fact]
    public void VoiceChannelViewModel_IsDragSource_RaisesPropertyChanged()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);
        var propertyChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VoiceChannelViewModel.IsDragSource))
                propertyChanged = true;
        };

        // Act
        vm.IsDragSource = true;

        // Assert
        Assert.True(propertyChanged);
        Assert.True(vm.IsDragSource);
    }

    #endregion

    #region VoiceChannelViewModel Dispose Tests

    [Fact]
    public void VoiceChannelViewModel_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var vm = new VoiceChannelViewModel(_channel, _currentUserId);

        // Act & Assert - should not throw
        vm.Dispose();
        vm.Dispose();
    }

    #endregion
}
