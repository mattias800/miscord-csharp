namespace Snacka.Shared.Models;

public class Channel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Topic { get; set; }
    public required Guid CommunityId { get; set; }
    public Community? Community { get; set; }
    public required ChannelType Type { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<VoiceParticipant> VoiceParticipants { get; set; } = new List<VoiceParticipant>();
    public ICollection<ChannelReadState> ChannelReadStates { get; set; } = new List<ChannelReadState>();
}

public enum ChannelType
{
    Text,
    Voice
}
