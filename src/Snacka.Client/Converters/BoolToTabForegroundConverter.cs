using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Snacka.Client.Converters;

/// <summary>
/// Converts a boolean (isSelected) to a tab foreground brush.
/// Selected = White, Unselected = Muted gray.
/// </summary>
public class BoolToTabForegroundConverter : IValueConverter
{
    public static readonly BoolToTabForegroundConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#ffffff"));
    private static readonly IBrush UnselectedBrush = new SolidColorBrush(Color.Parse("#949ba4"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
            return isSelected ? SelectedBrush : UnselectedBrush;
        return UnselectedBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
