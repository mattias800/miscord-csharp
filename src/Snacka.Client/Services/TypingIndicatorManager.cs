using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace Snacka.Client.Services;

/// <summary>
/// Represents a user who is currently typing.
/// </summary>
public record TypingUser(Guid UserId, string Username, DateTime LastTypingAt);

/// <summary>
/// Manages typing indicators for a chat context (channel or conversation).
/// Handles throttling outgoing typing events and expiring stale indicators.
/// </summary>
public class TypingIndicatorManager : IDisposable
{
    private const int TypingThrottleMs = 3000; // Send typing event every 3 seconds
    private const int TypingTimeoutMs = 5000; // Clear typing after 5 seconds of inactivity

    private readonly ObservableCollection<TypingUser> _typingUsers = new();
    private readonly System.Timers.Timer _cleanupTimer;
    private DateTime _lastTypingSent = DateTime.MinValue;

    public TypingIndicatorManager()
    {
        _cleanupTimer = new System.Timers.Timer(1000); // Check every second
        _cleanupTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(CleanupExpiredIndicators);
        _cleanupTimer.Start();
    }

    /// <summary>
    /// Collection of users currently typing.
    /// </summary>
    public ObservableCollection<TypingUser> TypingUsers => _typingUsers;

    /// <summary>
    /// Whether anyone is currently typing.
    /// </summary>
    public bool IsAnyoneTyping => _typingUsers.Count > 0;

    /// <summary>
    /// Formatted text showing who is typing.
    /// </summary>
    public string IndicatorText => FormatIndicatorText();

    /// <summary>
    /// Event raised when the typing indicator state changes.
    /// </summary>
    public event Action? IndicatorChanged;

    /// <summary>
    /// Called when a user starts typing. Updates or adds them to the list.
    /// </summary>
    public void OnUserTyping(Guid userId, string username)
    {
        var existing = _typingUsers.FirstOrDefault(t => t.UserId == userId);
        if (existing != null)
            _typingUsers.Remove(existing);

        _typingUsers.Add(new TypingUser(userId, username, DateTime.UtcNow));
        IndicatorChanged?.Invoke();
    }

    /// <summary>
    /// Called when a user sends a message. Removes them from the typing list.
    /// </summary>
    public void OnUserSentMessage(Guid userId)
    {
        var typingUser = _typingUsers.FirstOrDefault(t => t.UserId == userId);
        if (typingUser != null)
        {
            _typingUsers.Remove(typingUser);
            IndicatorChanged?.Invoke();
        }
    }

    /// <summary>
    /// Checks if enough time has passed to send another typing event.
    /// Returns true if a typing event should be sent.
    /// </summary>
    public bool ShouldSendTypingEvent()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTypingSent).TotalMilliseconds > TypingThrottleMs)
        {
            _lastTypingSent = now;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all typing indicators.
    /// </summary>
    public void Clear()
    {
        if (_typingUsers.Count > 0)
        {
            _typingUsers.Clear();
            IndicatorChanged?.Invoke();
        }
    }

    private void CleanupExpiredIndicators()
    {
        var now = DateTime.UtcNow;
        var expired = _typingUsers
            .Where(t => (now - t.LastTypingAt).TotalMilliseconds > TypingTimeoutMs)
            .ToList();

        if (expired.Count == 0) return;

        foreach (var user in expired)
            _typingUsers.Remove(user);

        IndicatorChanged?.Invoke();
    }

    private string FormatIndicatorText()
    {
        return _typingUsers.Count switch
        {
            0 => string.Empty,
            1 => $"{_typingUsers[0].Username} is typing...",
            2 => $"{_typingUsers[0].Username} and {_typingUsers[1].Username} are typing...",
            _ => $"{_typingUsers[0].Username} and {_typingUsers.Count - 1} others are typing..."
        };
    }

    public void Dispose()
    {
        _cleanupTimer.Stop();
        _cleanupTimer.Dispose();
    }
}
