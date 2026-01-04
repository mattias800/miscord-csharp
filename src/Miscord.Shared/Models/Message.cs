namespace Miscord.Shared.Models;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Content { get; set; }
    public required Guid AuthorId { get; set; }
    public User? Author { get; set; }
    public required Guid ChannelId { get; set; }
    public Channel? Channel { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsEdited => UpdatedAt > CreatedAt;
}
