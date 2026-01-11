using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Snacka.Client.Converters;

/// <summary>
/// Converts axis value (-1 to 1) to a position within a given size.
/// ConverterParameter is the size (e.g., 100 for a 100px canvas).
/// </summary>
public class AxisToPositionConverter : IValueConverter
{
    public static readonly AxisToPositionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is float axisValue && parameter is string sizeStr && double.TryParse(sizeStr, out var size))
        {
            // Map -1..1 to 0..size, centering at size/2
            // For stick visualization: account for indicator size (20px)
            var position = ((axisValue + 1) / 2.0) * size;
            return position;
        }

        if (value is float axis && parameter is int sizeInt)
        {
            var position = ((axis + 1) / 2.0) * sizeInt;
            return position;
        }

        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to button background color.
/// </summary>
public class BoolToButtonColorConverter : IValueConverter
{
    public static readonly BoolToButtonColorConverter Instance = new();

    private static readonly IBrush PressedBrush = new SolidColorBrush(Color.Parse("#5865f2"));
    private static readonly IBrush ReleasedBrush = new SolidColorBrush(Color.Parse("#40444b"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool pressed)
        {
            return pressed ? PressedBrush : ReleasedBrush;
        }
        return ReleasedBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts hat switch value to D-pad direction colors.
/// Common hat switch values:
/// -1 = centered, 0 = N, 1 = NE, 2 = E, 3 = SE, 4 = S, 5 = SW, 6 = W, 7 = NW
/// </summary>
public class HatToDPadConverter : IValueConverter
{
    public static readonly HatToDPadConverter Up = new(HatDirection.Up);
    public static readonly HatToDPadConverter Down = new(HatDirection.Down);
    public static readonly HatToDPadConverter Left = new(HatDirection.Left);
    public static readonly HatToDPadConverter Right = new(HatDirection.Right);

    private enum HatDirection { Up, Down, Left, Right }

    private readonly HatDirection _direction;
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#5865f2"));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#40444b"));

    private HatToDPadConverter(HatDirection direction)
    {
        _direction = direction;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int hatValue)
            return InactiveBrush;

        // Common hat switch mappings (0-indexed, 8 positions)
        // Some controllers use 0-7, others use 1-8 or different scales
        var isActive = _direction switch
        {
            HatDirection.Up => hatValue == 0 || hatValue == 1 || hatValue == 7,      // N, NE, NW
            HatDirection.Right => hatValue == 1 || hatValue == 2 || hatValue == 3,   // NE, E, SE
            HatDirection.Down => hatValue == 3 || hatValue == 4 || hatValue == 5,    // SE, S, SW
            HatDirection.Left => hatValue == 5 || hatValue == 6 || hatValue == 7,    // SW, W, NW
            _ => false
        };

        return isActive ? ActiveBrush : InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
