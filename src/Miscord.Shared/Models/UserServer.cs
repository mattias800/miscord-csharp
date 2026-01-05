namespace Miscord.Shared.Models;

public class UserCommunity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }
    public User? User { get; set; }
    public required Guid CommunityId { get; set; }
    public Community? Community { get; set; }
    public required UserRole Role { get; set; } = UserRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public enum UserRole
{
    Owner,
    Admin,
    Moderator,
    Member
}
