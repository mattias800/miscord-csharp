namespace Miscord.Shared.Models;

public class UserServer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }
    public User? User { get; set; }
    public required Guid ServerId { get; set; }
    public MiscordServer? Server { get; set; }
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
