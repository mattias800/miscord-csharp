namespace Miscord.Shared.Models;

public class DirectMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Content { get; set; }
    public required Guid SenderId { get; set; }
    public User? Sender { get; set; }
    public required Guid RecipientId { get; set; }
    public User? Recipient { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
