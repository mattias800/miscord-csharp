using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Snacka.Client.Converters;

/// <summary>
/// Converts a boolean IsOnline status to a color.
/// Online = green, offline = gray.
/// </summary>
public class OnlineStatusColorConverter : IValueConverter
{
    public static readonly OnlineStatusColorConverter Instance = new();

    private static readonly IBrush OnlineBrush = new SolidColorBrush(Color.Parse("#3ba55c"));
    private static readonly IBrush OfflineBrush = new SolidColorBrush(Color.Parse("#747f8d"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOnline)
            return isOnline ? OnlineBrush : OfflineBrush;
        return OfflineBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
