using System.Globalization;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;

namespace Snacka.Client.Converters;

/// <summary>
/// Converts a boolean IsExpanded value to a chevron Symbol.
/// Expanded = ChevronDown, Collapsed = ChevronRight.
/// </summary>
public class ExpandedToChevronConverter : IValueConverter
{
    public static readonly ExpandedToChevronConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
            return isExpanded ? Symbol.ChevronDown : Symbol.ChevronRight;
        return Symbol.ChevronRight;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
