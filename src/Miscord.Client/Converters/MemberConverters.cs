using System.Globalization;
using Avalonia.Data.Converters;

namespace Miscord.Client.Converters;

/// <summary>
/// Compares two Guids and returns true if they are equal.
/// Used with MultiBinding to check if a member is the current user.
/// Usage:
/// <MultiBinding Converter="{x:Static conv:GuidEqualsConverter.Instance}">
///     <Binding Path="UserId"/>
///     <Binding Path="#MembersList.DataContext.UserId"/>
/// </MultiBinding>
/// </summary>
public class GuidEqualsConverter : IMultiValueConverter
{
    public static readonly GuidEqualsConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is Guid guid1 && values[1] is Guid guid2)
        {
            return guid1 == guid2;
        }
        return false;
    }
}

/// <summary>
/// Compares two Guids and returns true if they are NOT equal.
/// Used to show elements only for other users (not the current user).
/// </summary>
public class GuidNotEqualsConverter : IMultiValueConverter
{
    public static readonly GuidNotEqualsConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is Guid guid1 && values[1] is Guid guid2)
        {
            return guid1 != guid2;
        }
        return true;
    }
}
