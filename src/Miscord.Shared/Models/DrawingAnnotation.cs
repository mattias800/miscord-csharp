namespace Miscord.Shared.Models;

/// <summary>
/// A point with float coordinates, normalized to 0.0-1.0 range.
/// This ensures drawings align correctly across different screen sizes.
/// </summary>
public struct PointF
{
    /// <summary>X coordinate (0.0 = left edge, 1.0 = right edge)</summary>
    public float X { get; set; }

    /// <summary>Y coordinate (0.0 = top edge, 1.0 = bottom edge)</summary>
    public float Y { get; set; }

    public PointF(float x, float y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// A single drawing stroke (one mouse down -> drag -> mouse up).
/// </summary>
public class DrawingStroke
{
    /// <summary>Unique identifier for this stroke</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User who drew this stroke</summary>
    public required Guid UserId { get; set; }

    /// <summary>Username for display</summary>
    public required string Username { get; set; }

    /// <summary>Points along the stroke path (normalized 0.0-1.0 coordinates)</summary>
    public required List<PointF> Points { get; set; }

    /// <summary>Stroke color as hex string (e.g., "#FF0000")</summary>
    public required string Color { get; set; }

    /// <summary>Stroke thickness (in normalized units, typically 1-10)</summary>
    public float Thickness { get; set; } = 3.0f;

    /// <summary>When the stroke was created</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Message sent via SignalR for annotation synchronization.
/// </summary>
public class AnnotationMessage
{
    /// <summary>Voice channel where the screen share is happening</summary>
    public required Guid ChannelId { get; set; }

    /// <summary>User who is sharing their screen</summary>
    public required Guid SharerUserId { get; set; }

    /// <summary>
    /// Action type:
    /// - "stroke" (completed stroke)
    /// - "stroke_update" (live update while drawing - replace stroke with same ID)
    /// - "erase" (remove stroke)
    /// - "clear" (remove all)
    /// - "allow_drawing" (host allows/disallows drawing by viewers)
    /// </summary>
    public required string Action { get; set; }

    /// <summary>The stroke data (for "stroke" and "stroke_update" actions)</summary>
    public DrawingStroke? Stroke { get; set; }

    /// <summary>ID of stroke to erase (for "erase" action)</summary>
    public Guid? EraseStrokeId { get; set; }

    /// <summary>Whether drawing is allowed (for "allow_drawing" action)</summary>
    public bool? IsDrawingAllowed { get; set; }
}
