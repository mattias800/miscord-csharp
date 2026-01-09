using System.Collections.ObjectModel;
using Miscord.Client.Services;
using ReactiveUI;

namespace Miscord.Client.ViewModels;

/// <summary>
/// Wrapper for VoiceParticipantResponse that adds reactive IsSpeaking state and per-user volume control.
/// </summary>
public class VoiceParticipantViewModel : ReactiveObject
{
    private bool _isSpeaking;
    private float _volume = 1.0f;
    private readonly Guid _currentUserId;
    private readonly Action<Guid, float>? _onVolumeChanged;

    public VoiceParticipantViewModel(VoiceParticipantResponse participant, Guid currentUserId, float initialVolume = 1.0f, Action<Guid, float>? onVolumeChanged = null)
    {
        Participant = participant;
        _currentUserId = currentUserId;
        _volume = Math.Clamp(initialVolume, 0f, 2f);
        _onVolumeChanged = onVolumeChanged;
    }

    public VoiceParticipantResponse Participant { get; private set; }

    public Guid UserId => Participant.UserId;
    public string Username => Participant.Username;
    public bool IsMuted => Participant.IsMuted;
    public bool IsDeafened => Participant.IsDeafened;
    public bool IsCameraOn => Participant.IsCameraOn;

    /// <summary>
    /// Whether this participant is the current user.
    /// </summary>
    public bool IsCurrentUser => UserId == _currentUserId;

    public bool IsSpeaking
    {
        get => _isSpeaking;
        set => this.RaiseAndSetIfChanged(ref _isSpeaking, value);
    }

    /// <summary>
    /// Volume level for this user (0.0 to 2.0, where 1.0 is 100%).
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            var clamped = Math.Clamp(value, 0f, 2f);
            if (Math.Abs(_volume - clamped) > 0.001f)
            {
                this.RaiseAndSetIfChanged(ref _volume, clamped);
                _onVolumeChanged?.Invoke(UserId, clamped);
                this.RaisePropertyChanged(nameof(VolumePercent));
            }
        }
    }

    /// <summary>
    /// Volume as a percentage (0 to 200).
    /// </summary>
    public int VolumePercent
    {
        get => (int)(_volume * 100);
        set => Volume = value / 100f;
    }

    public void UpdateState(VoiceStateUpdate state)
    {
        Participant = Participant with
        {
            IsMuted = state.IsMuted ?? Participant.IsMuted,
            IsDeafened = state.IsDeafened ?? Participant.IsDeafened,
            IsScreenSharing = state.IsScreenSharing ?? Participant.IsScreenSharing,
            IsCameraOn = state.IsCameraOn ?? Participant.IsCameraOn
        };
        this.RaisePropertyChanged(nameof(IsMuted));
        this.RaisePropertyChanged(nameof(IsDeafened));
        this.RaisePropertyChanged(nameof(IsCameraOn));
    }
}

/// <summary>
/// Wrapper ViewModel for voice channels that provides reactive participant tracking.
/// This ensures proper UI updates when participants join/leave.
/// </summary>
public class VoiceChannelViewModel : ReactiveObject
{
    private readonly ChannelResponse _channel;
    private readonly Guid _currentUserId;
    private readonly Action<Guid, float>? _onVolumeChanged;
    private readonly Func<Guid, float>? _getInitialVolume;

    public VoiceChannelViewModel(ChannelResponse channel, Guid currentUserId = default, Action<Guid, float>? onVolumeChanged = null, Func<Guid, float>? getInitialVolume = null)
    {
        _channel = channel;
        _currentUserId = currentUserId;
        _onVolumeChanged = onVolumeChanged;
        _getInitialVolume = getInitialVolume;
        Participants = new ObservableCollection<VoiceParticipantViewModel>();
    }

    public Guid Id => _channel.Id;
    public string Name => _channel.Name;
    public ChannelResponse Channel => _channel;

    public ObservableCollection<VoiceParticipantViewModel> Participants { get; }

    public void AddParticipant(VoiceParticipantResponse participant)
    {
        if (!Participants.Any(p => p.UserId == participant.UserId))
        {
            Console.WriteLine($"VoiceChannelVM [{Name}]: Adding participant {participant.Username}");
            var initialVolume = _getInitialVolume?.Invoke(participant.UserId) ?? 1.0f;
            Participants.Add(new VoiceParticipantViewModel(participant, _currentUserId, initialVolume, _onVolumeChanged));
        }
        else
        {
            Console.WriteLine($"VoiceChannelVM [{Name}]: Participant {participant.Username} already exists");
        }
    }

    public void RemoveParticipant(Guid userId)
    {
        var participant = Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is not null)
        {
            Console.WriteLine($"VoiceChannelVM [{Name}]: Removing participant {participant.Username}");
            Participants.Remove(participant);
        }
        else
        {
            Console.WriteLine($"VoiceChannelVM [{Name}]: Participant with ID {userId} not found");
        }
    }

    public void UpdateParticipantState(Guid userId, VoiceStateUpdate state)
    {
        var participant = Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is not null)
        {
            participant.UpdateState(state);
            Console.WriteLine($"VoiceChannelVM [{Name}]: Updated state for {participant.Username}");
        }
    }

    public void UpdateSpeakingState(Guid userId, bool isSpeaking)
    {
        var participant = Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is not null)
        {
            participant.IsSpeaking = isSpeaking;
        }
    }

    public void SetParticipants(IEnumerable<VoiceParticipantResponse> participants)
    {
        Console.WriteLine($"VoiceChannelVM [{Name}]: Setting {participants.Count()} participants");
        Participants.Clear();
        foreach (var p in participants)
        {
            var initialVolume = _getInitialVolume?.Invoke(p.UserId) ?? 1.0f;
            Participants.Add(new VoiceParticipantViewModel(p, _currentUserId, initialVolume, _onVolumeChanged));
        }
    }
}
