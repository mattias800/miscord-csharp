using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Snacka.Client.Converters;

/// <summary>
/// Converts a boolean (isSelected) to a tab background brush.
/// Selected = Content2Brush color, Unselected = Transparent.
/// </summary>
public class BoolToTabBackgroundConverter : IValueConverter
{
    public static readonly BoolToTabBackgroundConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#2b2d31"));
    private static readonly IBrush UnselectedBrush = Brushes.Transparent;

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
