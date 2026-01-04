using Microsoft.EntityFrameworkCore;
using Miscord.Shared.Models;

namespace Miscord.Server.Data;

public sealed class MiscordDbContext : DbContext
{
    public MiscordDbContext(DbContextOptions<MiscordDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<MiscordServer> Servers => Set<MiscordServer>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<UserServer> UserServers => Set<UserServer>();
    public DbSet<VoiceParticipant> VoiceParticipants => Set<VoiceParticipant>();

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

        // Server configuration
        modelBuilder.Entity<MiscordServer>()
            .HasKey(s => s.Id);
        modelBuilder.Entity<MiscordServer>()
            .HasOne(s => s.Owner)
            .WithMany(u => u.OwnedServers)
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Channel configuration
        modelBuilder.Entity<Channel>()
            .HasKey(c => c.Id);
        modelBuilder.Entity<Channel>()
            .HasOne(c => c.Server)
            .WithMany(s => s.Channels)
            .HasForeignKey(c => c.ServerId)
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

        // UserServer configuration
        modelBuilder.Entity<UserServer>()
            .HasKey(us => us.Id);
        modelBuilder.Entity<UserServer>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserServers)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserServer>()
            .HasOne(us => us.Server)
            .WithMany(s => s.UserServers)
            .HasForeignKey(us => us.ServerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserServer>()
            .HasIndex(us => new { us.UserId, us.ServerId })
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
    }
}
