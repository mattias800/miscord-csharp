using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Snacka.Client.Converters;

/// <summary>
/// Converts an unread count to a foreground brush.
/// Unread > 0 = White, otherwise = Muted gray (same as channel text).
/// </summary>
public class UnreadToForegroundConverter : IValueConverter
{
    public static readonly UnreadToForegroundConverter Instance = new();

    private static readonly IBrush UnreadBrush = new SolidColorBrush(Color.Parse("#ffffff"));
    private static readonly IBrush ReadBrush = new SolidColorBrush(Color.Parse("#949ba4"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int unreadCount)
            return unreadCount > 0 ? UnreadBrush : ReadBrush;
        return ReadBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
