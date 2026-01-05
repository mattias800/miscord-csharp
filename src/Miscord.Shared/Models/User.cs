namespace Miscord.Shared.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? Avatar { get; set; }
    public string? Status { get; set; }
    public bool IsOnline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Community> OwnedCommunities { get; set; } = new List<Community>();
    public ICollection<UserCommunity> UserCommunities { get; set; } = new List<UserCommunity>();
    public ICollection<DirectMessage> SentMessages { get; set; } = new List<DirectMessage>();
    public ICollection<DirectMessage> ReceivedMessages { get; set; } = new List<DirectMessage>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<VoiceParticipant> VoiceParticipants { get; set; } = new List<VoiceParticipant>();
}
