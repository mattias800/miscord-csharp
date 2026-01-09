using System.Globalization;
using Avalonia.Data.Converters;

namespace Miscord.Client.Converters;

/// <summary>
/// Safely converts a string to its first character.
/// Returns "?" if the string is null or empty.
/// </summary>
public class FirstCharConverter : IValueConverter
{
    public static readonly FirstCharConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
            return str[0].ToString();
        return "?";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
