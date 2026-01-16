namespace Snacka.Shared.Models;

/// <summary>
/// Controller button flags matching Xbox layout.
/// Maps to PlayStation: A=Cross, B=Circle, X=Square, Y=Triangle
/// </summary>
[Flags]
public enum ControllerButtons : uint
{
    None = 0,
    A = 1 << 0,             // Cross on PlayStation
    B = 1 << 1,             // Circle
    X = 1 << 2,             // Square
    Y = 1 << 3,             // Triangle
    LeftBumper = 1 << 4,    // L1
    RightBumper = 1 << 5,   // R1
    Back = 1 << 6,          // Select/Share
    Start = 1 << 7,         // Options
    LeftStick = 1 << 8,     // L3
    RightStick = 1 << 9,    // R3
    DPadUp = 1 << 10,
    DPadDown = 1 << 11,
    DPadLeft = 1 << 12,
    DPadRight = 1 << 13,
    Guide = 1 << 14,        // Xbox/PS button
}

/// <summary>
/// Controller type for proper button mapping on host.
/// </summary>
public enum ControllerType : byte
{
    Generic = 0,
    Xbox360 = 1,
    XboxOne = 2,
    DualShock4 = 3,
    DualSense = 4
}

/// <summary>
/// Controller state message sent from guest to host via server.
/// Compact binary-friendly format for low latency transmission.
/// </summary>
public record ControllerStateMessage(
    Guid ChannelId,
    Guid HostUserId,        // The screen sharer receiving this input
    byte ControllerSlot,    // 0-3 for players 1-4
    uint Buttons,           // ControllerButtons bitfield
    short LeftStickX,       // -32768 to 32767
    short LeftStickY,
    short RightStickX,
    short RightStickY,
    byte LeftTrigger,       // 0 to 255
    byte RightTrigger,
    long Timestamp          // Milliseconds since epoch for latency measurement
);

/// <summary>
/// Event sent to host when a viewer requests controller access.
/// </summary>
public record ControllerAccessRequestedEvent(
    Guid ChannelId,
    Guid RequesterUserId,
    string RequesterUsername
);

/// <summary>
/// Event sent to guest when host accepts their controller access request.
/// </summary>
public record ControllerAccessAcceptedEvent(
    Guid ChannelId,
    Guid HostUserId,
    string HostUsername,
    byte ControllerSlot     // Assigned player slot (0-3)
);

/// <summary>
/// Event sent to guest when host declines their controller access request.
/// </summary>
public record ControllerAccessDeclinedEvent(
    Guid ChannelId,
    Guid HostUserId
);

/// <summary>
/// Event sent when controller access is stopped (by either party).
/// </summary>
public record ControllerAccessStoppedEvent(
    Guid ChannelId,
    Guid HostUserId,
    Guid GuestUserId,
    string Reason           // "host_stopped", "guest_stopped", "disconnected"
);

/// <summary>
/// Event sent to host when receiving controller state from a guest.
/// </summary>
public record ControllerStateReceivedEvent(
    Guid ChannelId,
    Guid GuestUserId,
    string GuestUsername,
    ControllerStateMessage State
);
