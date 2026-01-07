using Microsoft.EntityFrameworkCore;
using Miscord.Shared.Models;

namespace Miscord.Server.Data;

public sealed class MiscordDbContext : DbContext
{
    public MiscordDbContext(DbContextOptions<MiscordDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<UserCommunity> UserCommunities => Set<UserCommunity>();
    public DbSet<VoiceParticipant> VoiceParticipants => Set<VoiceParticipant>();
    public DbSet<ServerInvite> ServerInvites => Set<ServerInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>()
            .HasKey(u => u.Id);
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Community configuration
        modelBuilder.Entity<Community>()
            .HasKey(c => c.Id);
        modelBuilder.Entity<Community>()
            .HasOne(c => c.Owner)
            .WithMany(u => u.OwnedCommunities)
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Channel configuration
        modelBuilder.Entity<Channel>()
            .HasKey(c => c.Id);
        modelBuilder.Entity<Channel>()
            .HasOne(c => c.Community)
            .WithMany(com => com.Channels)
            .HasForeignKey(c => c.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);

        // Message configuration
        modelBuilder.Entity<Message>()
            .HasKey(m => m.Id);
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Author)
            .WithMany(u => u.Messages)
            .HasForeignKey(m => m.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Channel)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // DirectMessage configuration
        modelBuilder.Entity<DirectMessage>()
            .HasKey(dm => dm.Id);
        modelBuilder.Entity<DirectMessage>()
            .HasOne(dm => dm.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(dm => dm.SenderId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DirectMessage>()
            .HasOne(dm => dm.Recipient)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(dm => dm.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserCommunity configuration
        modelBuilder.Entity<UserCommunity>()
            .HasKey(uc => uc.Id);
        modelBuilder.Entity<UserCommunity>()
            .HasOne(uc => uc.User)
            .WithMany(u => u.UserCommunities)
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserCommunity>()
            .HasOne(uc => uc.Community)
            .WithMany(c => c.UserCommunities)
            .HasForeignKey(uc => uc.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserCommunity>()
            .HasIndex(uc => new { uc.UserId, uc.CommunityId })
            .IsUnique();

        // VoiceParticipant configuration
        modelBuilder.Entity<VoiceParticipant>()
            .HasKey(vp => vp.Id);
        modelBuilder.Entity<VoiceParticipant>()
            .HasOne(vp => vp.User)
            .WithMany(u => u.VoiceParticipants)
            .HasForeignKey(vp => vp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VoiceParticipant>()
            .HasOne(vp => vp.Channel)
            .WithMany(c => c.VoiceParticipants)
            .HasForeignKey(vp => vp.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServerInvite configuration
        modelBuilder.Entity<ServerInvite>()
            .HasKey(si => si.Id);
        modelBuilder.Entity<ServerInvite>()
            .HasIndex(si => si.Code)
            .IsUnique();
        modelBuilder.Entity<ServerInvite>()
            .HasOne(si => si.CreatedBy)
            .WithMany()
            .HasForeignKey(si => si.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        // User.InvitedBy self-referential relationship
        modelBuilder.Entity<User>()
            .HasOne(u => u.InvitedBy)
            .WithMany()
            .HasForeignKey(u => u.InvitedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
