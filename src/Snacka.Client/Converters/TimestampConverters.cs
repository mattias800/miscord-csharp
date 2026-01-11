using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Snacka.Client.Converters;

/// <summary>
/// Converts a DateTime to a relative time string (e.g., "Just now", "2 min ago", "Yesterday").
/// </summary>
public class RelativeTimestampConverter : IValueConverter
{
    public static readonly RelativeTimestampConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
            return string.Empty;

        var now = DateTime.Now;
        var diff = now - dateTime;

        if (diff.TotalSeconds < 60)
            return "Just now";

        if (diff.TotalMinutes < 60)
        {
            var minutes = (int)diff.TotalMinutes;
            return minutes == 1 ? "1 min ago" : $"{minutes} min ago";
        }

        if (diff.TotalHours < 24 && dateTime.Date == now.Date)
        {
            var hours = (int)diff.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        if (dateTime.Date == now.Date.AddDays(-1))
            return $"Yesterday at {dateTime:h:mm tt}";

        if (diff.TotalDays < 7)
            return $"{dateTime:dddd} at {dateTime:h:mm tt}";

        return dateTime.ToString("MMM d, yyyy h:mm tt");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>
/// Returns the full timestamp for tooltip display.
/// </summary>
public class FullTimestampConverter : IValueConverter
{
    public static readonly FullTimestampConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
            return string.Empty;

        return dateTime.ToString("dddd, MMMM d, yyyy h:mm:ss tt");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>
/// Determines if a message should show a date separator (first message of its day).
/// Parameters: [0] = Current message, [1] = Messages collection
/// Compares CreatedAt property with the previous message to determine if dates differ.
/// </summary>
public class ShowDateSeparatorConverter : IMultiValueConverter
{
    public static readonly ShowDateSeparatorConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not { } message || values[1] == null)
            return false;

        // Get CreatedAt from the message using reflection (works with any message type)
        var createdAtProp = message.GetType().GetProperty("CreatedAt");
        if (createdAtProp == null)
            return false;

        var messageDate = (DateTime?)createdAtProp.GetValue(message);
        if (!messageDate.HasValue)
            return false;

        // Get the messages collection
        if (values[1] is not IList messages)
            return false;

        // Find index of current message
        var index = -1;
        for (var i = 0; i < messages.Count; i++)
        {
            if (ReferenceEquals(messages[i], message))
            {
                index = i;
                break;
            }
        }

        // First message always shows date separator
        if (index <= 0)
            return true;

        // Get previous message's date
        var prevMessage = messages[index - 1];
        if (prevMessage == null)
            return true;

        var prevCreatedAtProp = prevMessage.GetType().GetProperty("CreatedAt");
        if (prevCreatedAtProp == null)
            return true;

        var prevDateValue = prevCreatedAtProp.GetValue(prevMessage);
        if (prevDateValue is not DateTime prevDate)
            return true;

        // Show separator if dates are different
        return messageDate.Value.Date != prevDate.Date;
    }
}

/// <summary>
/// Formats a date for the date separator display.
/// Shows "Today", "Yesterday", or the full date.
/// </summary>
public class DateSeparatorTextConverter : IValueConverter
{
    public static readonly DateSeparatorTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
            return string.Empty;

        var today = DateTime.Today;
        var messageDate = dateTime.Date;

        if (messageDate == today)
            return "Today";

        if (messageDate == today.AddDays(-1))
            return "Yesterday";

        // Show full date for older messages
        return dateTime.ToString("MMMM d, yyyy");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
