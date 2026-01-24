using Snacka.Client.Services;
using Snacka.Client.Services.WebRtc;
using Snacka.Client.Stores;
using Snacka.Shared.Models;

namespace Snacka.Client.Coordinators;

/// <summary>
/// Result of joining a voice channel.
/// </summary>
public record VoiceJoinResult(
    bool Success,
    IReadOnlyList<VoiceParticipantResponse>? Participants = null);

/// <summary>
/// Coordinator for voice channel operations.
/// Handles joining/leaving voice, mute/deafen, screen sharing, and camera.
/// Also manages WebRTC connections for voice/video/screen share.
/// </summary>
public interface IVoiceCoordinator
{
    /// <summary>
    /// Joins a voice channel. Returns participants if successful.
    /// Handles SignalR join, store updates, and WebRTC connections.
    /// </summary>
    Task<VoiceJoinResult> JoinVoiceChannelAsync(Guid channelId);

    /// <summary>
    /// Joins a voice channel (simple overload for compatibility).
    /// </summary>
    Task<bool> JoinAsync(Guid channelId);

    /// <summary>
    /// Leaves the current voice channel.
    /// Handles SignalR leave, store cleanup, and WebRTC disconnection.
    /// </summary>
    Task LeaveVoiceChannelAsync();

    /// <summary>
    /// Leaves the current voice channel, optionally stopping screen share first.
    /// </summary>
    Task LeaveVoiceChannelAsync(bool stopScreenShare);

    /// <summary>
    /// Toggles local mute state.
    /// </summary>
    Task ToggleMuteAsync();

    /// <summary>
    /// Sets local mute state.
    /// </summary>
    Task SetMutedAsync(bool muted);

    /// <summary>
    /// Toggles local deafen state.
    /// </summary>
    Task ToggleDeafenAsync();

    /// <summary>
    /// Sets local deafen state.
    /// </summary>
    Task SetDeafenedAsync(bool deafened);

    /// <summary>
    /// Toggles camera on/off.
    /// </summary>
    Task ToggleCameraAsync();

    /// <summary>
    /// Toggles screen sharing on/off.
    /// </summary>
    Task ToggleScreenShareAsync();

    /// <summary>
    /// Starts screen sharing with specific settings.
    /// </summary>
    Task<bool> StartScreenShareAsync(bool withAudio);

    /// <summary>
    /// Stops screen sharing.
    /// </summary>
    Task StopScreenShareAsync();

    /// <summary>
    /// Updates speaking state.
    /// </summary>
    Task UpdateSpeakingStateAsync(bool isSpeaking);

    /// <summary>
    /// Server-mutes a user (admin action).
    /// </summary>
    Task<bool> ServerMuteUserAsync(Guid channelId, Guid userId, bool muted);

    /// <summary>
    /// Server-deafens a user (admin action).
    /// </summary>
    Task<bool> ServerDeafenUserAsync(Guid channelId, Guid userId, bool deafened);

    /// <summary>
    /// Moves a user to another voice channel (admin action).
    /// </summary>
    Task<bool> MoveUserAsync(Guid userId, Guid targetChannelId);

    /// <summary>
    /// Loads participants for a voice channel.
    /// </summary>
    Task LoadVoiceParticipantsAsync(Guid channelId);

    /// <summary>
    /// Handles the current user being moved to a different voice channel by an admin.
    /// Leaves current channel and joins the target channel.
    /// </summary>
    Task<bool> HandleCurrentUserMovedAsync(Guid targetChannelId);
}

public class VoiceCoordinator : IVoiceCoordinator
{
    private readonly IVoiceStore _voiceStore;
    private readonly IChannelStore _channelStore;
    private readonly IApiClient _apiClient;
    private readonly ISignalRService _signalR;
    private readonly IWebRtcService _webRtc;
    private readonly ISettingsStore _settingsStore;
    private readonly Guid _currentUserId;

    private Guid? _currentChannelId;

    public VoiceCoordinator(
        IVoiceStore voiceStore,
        IChannelStore channelStore,
        IApiClient apiClient,
        ISignalRService signalR,
        IWebRtcService webRtc,
        ISettingsStore settingsStore,
        Guid currentUserId)
    {
        _voiceStore = voiceStore;
        _channelStore = channelStore;
        _apiClient = apiClient;
        _signalR = signalR;
        _webRtc = webRtc;
        _settingsStore = settingsStore;
        _currentUserId = currentUserId;

        // Initialize local state from settings
        _voiceStore.SetLocalMuted(_settingsStore.Settings.IsMuted);
        _voiceStore.SetLocalDeafened(_settingsStore.Settings.IsDeafened);
    }

    public async Task<VoiceJoinResult> JoinVoiceChannelAsync(Guid channelId)
    {
        var channel = _channelStore.GetChannel(channelId);
        if (channel is null)
            return new VoiceJoinResult(false);

        try
        {
            _voiceStore.SetConnectionStatus(VoiceConnectionStatus.Connecting);

            // Join via SignalR
            var participant = await _signalR.JoinVoiceChannelAsync(channelId);
            if (participant is null)
            {
                _voiceStore.SetConnectionStatus(VoiceConnectionStatus.Disconnected);
                return new VoiceJoinResult(false);
            }

            // Update store state
            _currentChannelId = channelId;
            _voiceStore.SetCurrentChannel(channelId);
            _voiceStore.AddParticipant(participant);

            // Load all participants
            var participants = await _signalR.GetVoiceParticipantsAsync(channelId);
            var participantList = participants.ToList();
            _voiceStore.SetParticipants(channelId, participantList);

            // Start WebRTC connections to all existing participants
            await _webRtc.JoinVoiceChannelAsync(channelId, participantList);

            _voiceStore.SetConnectionStatus(VoiceConnectionStatus.Connected);
            return new VoiceJoinResult(true, participantList);
        }
        catch
        {
            _voiceStore.SetConnectionStatus(VoiceConnectionStatus.Disconnected);
            return new VoiceJoinResult(false);
        }
    }

    public async Task<bool> JoinAsync(Guid channelId)
    {
        var result = await JoinVoiceChannelAsync(channelId);
        return result.Success;
    }

    public Task LeaveVoiceChannelAsync() => LeaveVoiceChannelAsync(stopScreenShare: true);

    public async Task LeaveVoiceChannelAsync(bool stopScreenShare)
    {
        var channelId = GetCurrentChannelId();
        if (channelId is null)
            return;

        try
        {
            // Stop screen sharing if requested
            if (stopScreenShare)
            {
                await _webRtc.SetScreenSharingAsync(false);
            }

            // Leave WebRTC connections
            await _webRtc.LeaveVoiceChannelAsync();

            // Leave SignalR
            await _signalR.LeaveVoiceChannelAsync(channelId.Value);
        }
        catch
        {
            // Ignore errors, we're leaving anyway
        }
        finally
        {
            _currentChannelId = null;
            _voiceStore.SetCurrentChannel(null);
            _voiceStore.SetConnectionStatus(VoiceConnectionStatus.Disconnected);
            _voiceStore.ClearChannel(channelId.Value);
        }
    }

    public async Task ToggleMuteAsync()
    {
        var currentMuted = _settingsStore.Settings.IsMuted;
        await SetMutedAsync(!currentMuted);
    }

    public async Task SetMutedAsync(bool muted)
    {
        _voiceStore.SetLocalMuted(muted);
        _settingsStore.Settings.IsMuted = muted;
        _settingsStore.Save();

        var channelId = GetCurrentChannelId();
        if (channelId.HasValue)
        {
            try
            {
                await _signalR.UpdateVoiceStateAsync(channelId.Value, new VoiceStateUpdate(IsMuted: muted));
            }
            catch
            {
                // Voice state update failed, but local state is already updated
            }
        }
    }

    public async Task ToggleDeafenAsync()
    {
        var currentDeafened = _settingsStore.Settings.IsDeafened;
        await SetDeafenedAsync(!currentDeafened);
    }

    public async Task SetDeafenedAsync(bool deafened)
    {
        _voiceStore.SetLocalDeafened(deafened);
        _settingsStore.Settings.IsDeafened = deafened;
        _settingsStore.Save();

        // If deafening, also mute
        if (deafened && !_settingsStore.Settings.IsMuted)
        {
            _voiceStore.SetLocalMuted(true);
            _settingsStore.Settings.IsMuted = true;
            _settingsStore.Save();
        }

        var channelId = GetCurrentChannelId();
        if (channelId.HasValue)
        {
            try
            {
                var update = deafened
                    ? new VoiceStateUpdate(IsMuted: true, IsDeafened: true)
                    : new VoiceStateUpdate(IsDeafened: false);
                await _signalR.UpdateVoiceStateAsync(channelId.Value, update);
            }
            catch
            {
                // Voice state update failed, but local state is already updated
            }
        }
    }

    public async Task ToggleCameraAsync()
    {
        var participant = _voiceStore.GetLocalParticipant(_currentUserId);
        var currentCameraOn = participant?.IsCameraOn ?? false;

        _voiceStore.SetLocalCameraOn(!currentCameraOn);

        var channelId = GetCurrentChannelId();
        if (channelId.HasValue)
        {
            try
            {
                await _signalR.UpdateVoiceStateAsync(channelId.Value, new VoiceStateUpdate(IsCameraOn: !currentCameraOn));
            }
            catch
            {
                // Revert on failure
                _voiceStore.SetLocalCameraOn(currentCameraOn);
            }
        }
    }

    public async Task ToggleScreenShareAsync()
    {
        var participant = _voiceStore.GetLocalParticipant(_currentUserId);
        if (participant?.IsScreenSharing == true)
        {
            await StopScreenShareAsync();
        }
        else
        {
            await StartScreenShareAsync(withAudio: false);
        }
    }

    public async Task<bool> StartScreenShareAsync(bool withAudio)
    {
        var channelId = GetCurrentChannelId();
        if (!channelId.HasValue)
            return false;

        try
        {
            _voiceStore.SetLocalScreenSharing(true);
            await _signalR.UpdateVoiceStateAsync(channelId.Value, new VoiceStateUpdate(
                IsScreenSharing: true,
                ScreenShareHasAudio: withAudio));
            return true;
        }
        catch
        {
            _voiceStore.SetLocalScreenSharing(false);
            return false;
        }
    }

    public async Task StopScreenShareAsync()
    {
        var channelId = GetCurrentChannelId();
        if (!channelId.HasValue)
            return;

        _voiceStore.SetLocalScreenSharing(false);

        try
        {
            await _signalR.UpdateVoiceStateAsync(channelId.Value, new VoiceStateUpdate(
                IsScreenSharing: false,
                ScreenShareHasAudio: false));
        }
        catch
        {
            // Already stopped locally
        }
    }

    public async Task UpdateSpeakingStateAsync(bool isSpeaking)
    {
        _voiceStore.SetLocalSpeaking(isSpeaking);

        var channelId = GetCurrentChannelId();
        if (channelId.HasValue)
        {
            try
            {
                await _signalR.UpdateSpeakingStateAsync(channelId.Value, isSpeaking);
            }
            catch
            {
                // Ignore speaking state update failures
            }
        }
    }

    public async Task<bool> ServerMuteUserAsync(Guid channelId, Guid userId, bool muted)
    {
        try
        {
            await _signalR.ServerMuteUserAsync(channelId, userId, muted);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ServerDeafenUserAsync(Guid channelId, Guid userId, bool deafened)
    {
        try
        {
            await _signalR.ServerDeafenUserAsync(channelId, userId, deafened);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> MoveUserAsync(Guid userId, Guid targetChannelId)
    {
        try
        {
            await _signalR.MoveUserAsync(userId, targetChannelId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LoadVoiceParticipantsAsync(Guid channelId)
    {
        try
        {
            var participants = await _signalR.GetVoiceParticipantsAsync(channelId);
            _voiceStore.SetParticipants(channelId, participants.ToList());
        }
        catch
        {
            // Failed to load participants
        }
    }

    public async Task<bool> HandleCurrentUserMovedAsync(Guid targetChannelId)
    {
        // Leave current channel first
        await LeaveVoiceChannelAsync(stopScreenShare: true);

        // Join the target channel
        var result = await JoinVoiceChannelAsync(targetChannelId);
        return result.Success;
    }

    private Guid? GetCurrentChannelId()
    {
        return _currentChannelId;
    }
}
