namespace Miscord.Shared.Models;

/// <summary>
/// Tracks the last read message for a user in a channel.
/// Used for showing unread indicators in the channel list.
/// </summary>
public class ChannelReadState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The user who read the channel
    /// </summary>
    public required Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// The channel that was read
    /// </summary>
    public required Guid ChannelId { get; set; }
    public Channel? Channel { get; set; }

    /// <summary>
    /// The ID of the last message that was read
    /// </summary>
    public Guid? LastReadMessageId { get; set; }
    public Message? LastReadMessage { get; set; }

    /// <summary>
    /// Timestamp of when the channel was last read
    /// </summary>
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
}
