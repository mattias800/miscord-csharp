using System.Collections.Concurrent;

namespace Snacka.Client.Services.WebRtc;

/// <summary>
/// Manages audio output/playback, mixing, and per-user volume control.
/// Extracted from WebRtcService for single responsibility.
/// </summary>
public class AudioOutputManager : IAsyncDisposable
{
    private readonly ISettingsStore? _settingsStore;

    private IUserAudioMixer? _audioMixer;
    private bool _isDeafened;

    // SSRC to UserId mappings for routing audio to correct user mixer channels
    private readonly ConcurrentDictionary<uint, Guid> _audioSsrcToUserMap = new();
    private readonly ConcurrentDictionary<uint, Guid> _screenAudioSsrcToUserMap = new();

    /// <summary>
    /// Gets whether audio output is deafened (muted speaker).
    /// </summary>
    public bool IsDeafened => _isDeafened;

    /// <summary>
    /// Gets the underlying audio mixer for processing audio packets.
    /// Used by WebRtcService to route received audio to playback.
    /// </summary>
    public IUserAudioMixer? AudioMixer => _audioMixer;

    /// <summary>
    /// Gets the microphone audio SSRC to UserId mapping.
    /// </summary>
    public ConcurrentDictionary<uint, Guid> AudioSsrcToUserMap => _audioSsrcToUserMap;

    /// <summary>
    /// Gets the screen audio SSRC to UserId mapping.
    /// </summary>
    public ConcurrentDictionary<uint, Guid> ScreenAudioSsrcToUserMap => _screenAudioSsrcToUserMap;

    public AudioOutputManager(ISettingsStore? settingsStore)
    {
        _settingsStore = settingsStore;
    }

    /// <summary>
    /// Initializes the audio mixer for playback.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_audioMixer != null) return;

        try
        {
            var outputDevice = _settingsStore?.Settings.AudioOutputDevice ?? string.Empty;
            _audioMixer = new UserAudioMixer();
            await _audioMixer.StartAsync(outputDevice);

            // Load saved per-user volumes
            LoadUserVolumes();

            Console.WriteLine("AudioOutputManager: Audio mixer initialized with per-user volume control");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AudioOutputManager: Failed to initialize audio mixer: {ex.Message}");
            _audioMixer = null;
        }
    }

    /// <summary>
    /// Loads saved per-user volumes from settings.
    /// </summary>
    private void LoadUserVolumes()
    {
        if (_settingsStore?.Settings.UserVolumes == null || _audioMixer == null) return;

        foreach (var (userIdStr, volume) in _settingsStore.Settings.UserVolumes)
        {
            if (Guid.TryParse(userIdStr, out var userId))
            {
                _audioMixer.SetUserVolume(userId, volume);
            }
        }
        Console.WriteLine($"AudioOutputManager: Loaded {_settingsStore.Settings.UserVolumes.Count} saved user volumes");
    }

    /// <summary>
    /// Sets the volume for a specific user (0.0 - 2.0, where 1.0 is normal).
    /// </summary>
    public void SetUserVolume(Guid userId, float volume)
    {
        _audioMixer?.SetUserVolume(userId, volume);

        // Save to settings
        if (_settingsStore != null)
        {
            _settingsStore.Settings.UserVolumes[userId.ToString()] = volume;
            _settingsStore.Save();
        }
    }

    /// <summary>
    /// Gets the volume for a specific user.
    /// </summary>
    public float GetUserVolume(Guid userId)
    {
        // Try settings first (for users not currently in channel)
        if (_settingsStore?.Settings.UserVolumes.TryGetValue(userId.ToString(), out var savedVolume) == true)
        {
            return savedVolume;
        }
        return _audioMixer?.GetUserVolume(userId) ?? 1.0f;
    }

    /// <summary>
    /// Sets the deafened state (mutes all audio output).
    /// </summary>
    public void SetDeafened(bool deafened)
    {
        _isDeafened = deafened;
        Console.WriteLine($"AudioOutputManager: Deafened = {deafened}");

        // When deafened, we still receive audio but don't pass it to the mixer
        // This is handled by checking IsDeafened before processing packets
    }

    /// <summary>
    /// Clears SSRC mappings when leaving a channel.
    /// </summary>
    public void ClearSsrcMappings()
    {
        _audioSsrcToUserMap.Clear();
        _screenAudioSsrcToUserMap.Clear();
    }

    /// <summary>
    /// Stops the audio mixer.
    /// </summary>
    public async Task StopAsync()
    {
        if (_audioMixer != null)
        {
            try
            {
                await _audioMixer.StopAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AudioOutputManager: Error stopping mixer: {ex.Message}");
            }
            _audioMixer = null;
        }

        ClearSsrcMappings();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
