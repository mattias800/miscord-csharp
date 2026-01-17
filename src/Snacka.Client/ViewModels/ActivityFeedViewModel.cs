using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using Snacka.Client.Services;

namespace Snacka.Client.ViewModels;

/// <summary>
/// Type of activity/notification event.
/// </summary>
public enum ActivityType
{
    /// <summary>User joined the server (visible to admins/owners).</summary>
    UserJoinedServer,

    /// <summary>User joined a community (visible to all community members).</summary>
    UserJoinedCommunity,

    /// <summary>User left the server or community.</summary>
    UserLeft,

    /// <summary>User was mentioned in a channel or DM.</summary>
    Mention,

    /// <summary>New direct message received.</summary>
    DirectMessage,

    /// <summary>New message in a channel with unread messages.</summary>
    ChannelMessage,

    /// <summary>User was invited to a community.</summary>
    CommunityInvite,

    /// <summary>Thread reply received.</summary>
    ThreadReply
}

/// <summary>
/// Represents a single activity/notification item in the activity feed.
/// </summary>
public record ActivityItem(
    Guid Id,
    ActivityType Type,
    DateTime Timestamp,
    string Title,
    string Description,
    Guid? UserId = null,
    string? Username = null,
    Guid? CommunityId = null,
    string? CommunityName = null,
    Guid? ChannelId = null,
    string? ChannelName = null,
    Guid? MessageId = null,
    bool IsRead = false
)
{
    /// <summary>
    /// Returns the appropriate icon for this activity type.
    /// </summary>
    public string Icon => Type switch
    {
        ActivityType.UserJoinedServer => "PersonAdd",
        ActivityType.UserJoinedCommunity => "People",
        ActivityType.UserLeft => "PersonDelete",
        ActivityType.Mention => "Mention",
        ActivityType.DirectMessage => "Chat",
        ActivityType.ChannelMessage => "Comment",
        ActivityType.CommunityInvite => "Mail",
        ActivityType.ThreadReply => "CommentMultiple",
        _ => "Info"
    };

    /// <summary>
    /// Returns a relative time string (e.g., "2m ago", "1h ago").
    /// </summary>
    public string RelativeTime
    {
        get
        {
            var diff = DateTime.UtcNow - Timestamp;
            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return Timestamp.ToString("MMM d");
        }
    }
}

/// <summary>
/// ViewModel for the activity/notifications feed.
/// </summary>
public class ActivityFeedViewModel : ViewModelBase
{
    private readonly ISignalRService _signalR;
    private readonly IApiClient _apiClient;
    private readonly Guid _currentUserId;
    private readonly Func<Guid?> _getCurrentCommunityId;
    private readonly Func<bool> _canManageServer;

    private ObservableCollection<ActivityItem> _activities = new();
    private bool _isLoading;

    public ActivityFeedViewModel(
        ISignalRService signalR,
        IApiClient apiClient,
        Guid currentUserId,
        Func<Guid?> getCurrentCommunityId,
        Func<bool> canManageServer)
    {
        _signalR = signalR;
        _apiClient = apiClient;
        _currentUserId = currentUserId;
        _getCurrentCommunityId = getCurrentCommunityId;
        _canManageServer = canManageServer;

        // Commands
        MarkAllAsReadCommand = ReactiveCommand.Create(MarkAllAsRead);
        ClearAllCommand = ReactiveCommand.Create(ClearAll);

        SetupSignalRHandlers();
    }

    // Commands
    public ReactiveCommand<Unit, Unit> MarkAllAsReadCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearAllCommand { get; }

    public ObservableCollection<ActivityItem> Activities => _activities;

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public int UnreadCount => _activities.Count(a => !a.IsRead);

    public bool HasUnread => UnreadCount > 0;

    private void SetupSignalRHandlers()
    {
        // User joined community
        _signalR.CommunityMemberAdded += e => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var currentCommunityId = _getCurrentCommunityId();
            if (currentCommunityId == e.CommunityId)
            {
                AddActivity(new ActivityItem(
                    Guid.NewGuid(),
                    ActivityType.UserJoinedCommunity,
                    DateTime.UtcNow,
                    "New member joined",
                    "Welcome to the community!",
                    UserId: e.UserId,
                    CommunityId: e.CommunityId
                ));
            }
        });

        // Direct message received
        _signalR.DirectMessageReceived += message => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Only show notification if it's from someone else
            if (message.SenderId != _currentUserId)
            {
                AddActivity(new ActivityItem(
                    Guid.NewGuid(),
                    ActivityType.DirectMessage,
                    message.CreatedAt,
                    $"DM from {message.SenderEffectiveDisplayName}",
                    message.Content.Length > 50 ? message.Content[..50] + "..." : message.Content,
                    UserId: message.SenderId,
                    Username: message.SenderUsername,
                    MessageId: message.Id
                ));
            }
        });

        // Message received (for mentions)
        _signalR.MessageReceived += message => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Check if current user is mentioned (simple text-based check)
            // A more robust implementation would parse mentions server-side
            if (message.Content.Contains($"@") && message.AuthorId != _currentUserId)
            {
                AddActivity(new ActivityItem(
                    Guid.NewGuid(),
                    ActivityType.Mention,
                    message.CreatedAt,
                    $"Mentioned by {message.AuthorEffectiveDisplayName}",
                    message.Content.Length > 50 ? message.Content[..50] + "..." : message.Content,
                    UserId: message.AuthorId,
                    Username: message.AuthorUsername,
                    ChannelId: message.ChannelId,
                    MessageId: message.Id
                ));
            }
        });

        // Thread reply
        _signalR.ThreadReplyReceived += e => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Only notify if someone else replied
            if (e.Reply.AuthorId != _currentUserId)
            {
                AddActivity(new ActivityItem(
                    Guid.NewGuid(),
                    ActivityType.ThreadReply,
                    e.Reply.CreatedAt,
                    $"Reply from {e.Reply.AuthorEffectiveDisplayName}",
                    e.Reply.Content.Length > 50 ? e.Reply.Content[..50] + "..." : e.Reply.Content,
                    UserId: e.Reply.AuthorId,
                    Username: e.Reply.AuthorUsername,
                    MessageId: e.Reply.Id
                ));
            }
        });
    }

    private void AddActivity(ActivityItem activity)
    {
        // Insert at the beginning (most recent first)
        _activities.Insert(0, activity);

        // Limit to 50 items
        while (_activities.Count > 50)
            _activities.RemoveAt(_activities.Count - 1);

        this.RaisePropertyChanged(nameof(UnreadCount));
        this.RaisePropertyChanged(nameof(HasUnread));
    }

    /// <summary>
    /// Marks all activities as read.
    /// </summary>
    public void MarkAllAsRead()
    {
        for (int i = 0; i < _activities.Count; i++)
        {
            if (!_activities[i].IsRead)
            {
                _activities[i] = _activities[i] with { IsRead = true };
            }
        }
        this.RaisePropertyChanged(nameof(UnreadCount));
        this.RaisePropertyChanged(nameof(HasUnread));
    }

    /// <summary>
    /// Clears all activities.
    /// </summary>
    public void ClearAll()
    {
        _activities.Clear();
        this.RaisePropertyChanged(nameof(UnreadCount));
        this.RaisePropertyChanged(nameof(HasUnread));
    }
}
