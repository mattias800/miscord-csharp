using System.Timers;
using ReactiveUI;
using Snacka.Shared.Models;
using Timer = System.Timers.Timer;

namespace Snacka.Client.Services;

/// <summary>
/// Manages streaming controller input from guest to host via SignalR.
/// On the guest side, captures controller state and sends to the host.
/// </summary>
public interface IControllerStreamingService : IDisposable
{
    /// <summary>
    /// Whether we are currently streaming controller input to a host.
    /// </summary>
    bool IsStreaming { get; }

    /// <summary>
    /// The channel we are streaming in.
    /// </summary>
    Guid? StreamingChannelId { get; }

    /// <summary>
    /// The host we are streaming to.
    /// </summary>
    Guid? StreamingHostUserId { get; }

    /// <summary>
    /// The assigned controller slot (0-3).
    /// </summary>
    byte? AssignedSlot { get; }

    /// <summary>
    /// Request controller access from a host who is screen sharing.
    /// </summary>
    Task RequestAccessAsync(Guid channelId, Guid hostUserId);

    /// <summary>
    /// Stop streaming controller input.
    /// </summary>
    Task StopStreamingAsync();

    /// <summary>
    /// Fired when access is accepted and streaming starts.
    /// </summary>
    event Action<Guid, Guid, byte>? StreamingStarted; // channelId, hostUserId, slot

    /// <summary>
    /// Fired when streaming stops (declined, stopped by either party, or disconnected).
    /// </summary>
    event Action<string>? StreamingStopped; // reason
}

public class ControllerStreamingService : ReactiveObject, IControllerStreamingService
{
    private readonly ISignalRService _signalR;
    private readonly IControllerService _controllerService;
    private readonly ISettingsStore _settingsStore;
    private Timer? _streamingTimer;
    private bool _isStreaming;
    private Guid? _streamingChannelId;
    private Guid? _streamingHostUserId;
    private byte? _assignedSlot;

    // Streaming rate: 60Hz for responsive input
    private const int StreamingIntervalMs = 16; // ~60Hz

    public ControllerStreamingService(ISignalRService signalR, IControllerService controllerService, ISettingsStore settingsStore)
    {
        _signalR = signalR;
        _controllerService = controllerService;
        _settingsStore = settingsStore;

        // Subscribe to SignalR events
        _signalR.ControllerAccessAccepted += OnAccessAccepted;
        _signalR.ControllerAccessDeclined += OnAccessDeclined;
        _signalR.ControllerAccessStopped += OnAccessStopped;
        _signalR.ControllerRumbleReceived += OnRumbleReceived;

        // Subscribe to controller disconnect events
        _controllerService.ControllerDisconnected += OnControllerDisconnected;
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        private set => this.RaiseAndSetIfChanged(ref _isStreaming, value);
    }

    public Guid? StreamingChannelId
    {
        get => _streamingChannelId;
        private set => this.RaiseAndSetIfChanged(ref _streamingChannelId, value);
    }

    public Guid? StreamingHostUserId
    {
        get => _streamingHostUserId;
        private set => this.RaiseAndSetIfChanged(ref _streamingHostUserId, value);
    }

    public byte? AssignedSlot
    {
        get => _assignedSlot;
        private set => this.RaiseAndSetIfChanged(ref _assignedSlot, value);
    }

    public event Action<Guid, Guid, byte>? StreamingStarted;
    public event Action<string>? StreamingStopped;

    public async Task RequestAccessAsync(Guid channelId, Guid hostUserId)
    {
        if (IsStreaming)
        {
            Console.WriteLine("ControllerStreamingService: Already streaming, stop first");
            return;
        }

        // Store pending request info
        StreamingChannelId = channelId;
        StreamingHostUserId = hostUserId;

        Console.WriteLine($"ControllerStreamingService: Requesting access from host {hostUserId} in channel {channelId}");
        await _signalR.RequestControllerAccessAsync(channelId, hostUserId);
    }

    public async Task StopStreamingAsync()
    {
        if (!IsStreaming || !StreamingChannelId.HasValue || !StreamingHostUserId.HasValue)
        {
            Console.WriteLine("ControllerStreamingService: Not streaming");
            return;
        }

        Console.WriteLine($"ControllerStreamingService: Stopping streaming to host {StreamingHostUserId}");

        await _signalR.StopControllerAccessAsync(StreamingChannelId.Value, StreamingHostUserId.Value);
        CleanupStreaming("guest_stopped");
    }

    private void OnAccessAccepted(ControllerAccessAcceptedEvent e)
    {
        // Verify this is our pending request
        if (e.ChannelId != StreamingChannelId || e.HostUserId != StreamingHostUserId)
        {
            Console.WriteLine($"ControllerStreamingService: Received acceptance for different session, ignoring");
            return;
        }

        Console.WriteLine($"ControllerStreamingService: Access accepted! Assigned slot {e.ControllerSlot} (Player {e.ControllerSlot + 1})");

        AssignedSlot = e.ControllerSlot;
        IsStreaming = true;

        // Start the controller reading if not already
        if (_controllerService.SelectedController != null && !_controllerService.IsReading)
        {
            _controllerService.StartReading();
        }

        // Start streaming timer
        StartStreamingTimer();

        StreamingStarted?.Invoke(e.ChannelId, e.HostUserId, e.ControllerSlot);
    }

    private void OnAccessDeclined(ControllerAccessDeclinedEvent e)
    {
        if (e.ChannelId != StreamingChannelId || e.HostUserId != StreamingHostUserId)
        {
            return;
        }

        Console.WriteLine($"ControllerStreamingService: Access declined by host");
        CleanupStreaming("declined");
    }

    private void OnAccessStopped(ControllerAccessStoppedEvent e)
    {
        if (e.ChannelId != StreamingChannelId || e.HostUserId != StreamingHostUserId)
        {
            return;
        }

        Console.WriteLine($"ControllerStreamingService: Session stopped ({e.Reason})");
        CleanupStreaming(e.Reason);
    }

    private void OnControllerDisconnected(ControllerDevice controller)
    {
        if (!IsStreaming)
        {
            return;
        }

        // Controller disconnected while streaming - log warning but keep session open
        // The session will resume when the user reconnects and starts reading again
        Console.WriteLine($"ControllerStreamingService: Controller '{controller.Name}' disconnected while streaming!");
        Console.WriteLine("ControllerStreamingService: Session remains active - reconnect controller to resume.");

        // Note: We don't stop streaming here. The streaming timer will keep running,
        // but will send zeroed/default state until the controller is reconnected.
        // This allows seamless reconnection without needing to re-request access.
    }

    private void OnRumbleReceived(ControllerRumbleReceivedEvent e)
    {
        // Only process if we're streaming and this rumble is for our slot
        if (!IsStreaming || e.Rumble.ControllerSlot != AssignedSlot)
        {
            return;
        }

        // Check if rumble is enabled in settings
        if (!_settingsStore.Settings.ControllerRumbleEnabled)
        {
            return;
        }

        // Forward rumble to physical controller
        _controllerService.SetRumble(e.Rumble.LargeMotor, e.Rumble.SmallMotor);
    }

    private void StartStreamingTimer()
    {
        _streamingTimer?.Dispose();
        _streamingTimer = new Timer(StreamingIntervalMs);
        _streamingTimer.Elapsed += OnStreamingTick;
        _streamingTimer.AutoReset = true;
        _streamingTimer.Start();

        Console.WriteLine($"ControllerStreamingService: Started streaming at {1000 / StreamingIntervalMs}Hz");
    }

    private void OnStreamingTick(object? sender, ElapsedEventArgs e)
    {
        if (!IsStreaming || !StreamingChannelId.HasValue || !StreamingHostUserId.HasValue || !AssignedSlot.HasValue)
        {
            return;
        }

        // Convert ControllerState to ControllerStateMessage
        var state = _controllerService.CurrentState;
        var message = ConvertToMessage(state);

        // Fire and forget - don't await in timer callback
        _ = _signalR.SendControllerStateAsync(message);
    }

    private ControllerStateMessage ConvertToMessage(ControllerState state)
    {
        // Convert axes from -1..1 float to -32768..32767 short
        var leftStickX = (short)(state.Axes[0] * 32767);
        var leftStickY = (short)(state.Axes[1] * 32767);
        var rightStickX = (short)(state.Axes[2] * 32767);
        var rightStickY = (short)(state.Axes[5] * 32767);

        // Convert triggers from 0..1 float to 0..255 byte
        // Note: Many controllers put triggers on axes 3 and 4, but this varies
        var leftTrigger = (byte)Math.Clamp((state.Axes[3] + 1) * 127.5f, 0, 255);
        var rightTrigger = (byte)Math.Clamp((state.Axes[4] + 1) * 127.5f, 0, 255);

        // Convert buttons to bitfield
        uint buttons = 0;
        if (state.Buttons[0]) buttons |= (uint)ControllerButtons.A;
        if (state.Buttons[1]) buttons |= (uint)ControllerButtons.B;
        if (state.Buttons[2]) buttons |= (uint)ControllerButtons.X;
        if (state.Buttons[3]) buttons |= (uint)ControllerButtons.Y;
        if (state.Buttons[4]) buttons |= (uint)ControllerButtons.LeftBumper;
        if (state.Buttons[5]) buttons |= (uint)ControllerButtons.RightBumper;
        if (state.Buttons[6]) buttons |= (uint)ControllerButtons.Back;
        if (state.Buttons[7]) buttons |= (uint)ControllerButtons.Start;
        if (state.Buttons[8]) buttons |= (uint)ControllerButtons.LeftStick;
        if (state.Buttons[9]) buttons |= (uint)ControllerButtons.RightStick;
        if (state.Buttons[10]) buttons |= (uint)ControllerButtons.Guide;

        // Convert hat switch to D-pad buttons
        switch (state.HatSwitch)
        {
            case 0: // Up
                buttons |= (uint)ControllerButtons.DPadUp;
                break;
            case 1: // Up-Right
                buttons |= (uint)ControllerButtons.DPadUp | (uint)ControllerButtons.DPadRight;
                break;
            case 2: // Right
                buttons |= (uint)ControllerButtons.DPadRight;
                break;
            case 3: // Down-Right
                buttons |= (uint)ControllerButtons.DPadDown | (uint)ControllerButtons.DPadRight;
                break;
            case 4: // Down
                buttons |= (uint)ControllerButtons.DPadDown;
                break;
            case 5: // Down-Left
                buttons |= (uint)ControllerButtons.DPadDown | (uint)ControllerButtons.DPadLeft;
                break;
            case 6: // Left
                buttons |= (uint)ControllerButtons.DPadLeft;
                break;
            case 7: // Up-Left
                buttons |= (uint)ControllerButtons.DPadUp | (uint)ControllerButtons.DPadLeft;
                break;
        }

        return new ControllerStateMessage(
            StreamingChannelId!.Value,
            StreamingHostUserId!.Value,
            AssignedSlot!.Value,
            buttons,
            leftStickX,
            leftStickY,
            rightStickX,
            rightStickY,
            leftTrigger,
            rightTrigger,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );
    }

    private void CleanupStreaming(string reason)
    {
        _streamingTimer?.Stop();
        _streamingTimer?.Dispose();
        _streamingTimer = null;

        IsStreaming = false;
        StreamingChannelId = null;
        StreamingHostUserId = null;
        AssignedSlot = null;

        StreamingStopped?.Invoke(reason);
    }

    public void Dispose()
    {
        _streamingTimer?.Dispose();
        _signalR.ControllerAccessAccepted -= OnAccessAccepted;
        _signalR.ControllerAccessDeclined -= OnAccessDeclined;
        _signalR.ControllerAccessStopped -= OnAccessStopped;
        _signalR.ControllerRumbleReceived -= OnRumbleReceived;
        _controllerService.ControllerDisconnected -= OnControllerDisconnected;
    }
}
