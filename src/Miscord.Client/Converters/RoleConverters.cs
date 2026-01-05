using System.Globalization;
using Avalonia.Data.Converters;
using Miscord.Shared.Models;

namespace Miscord.Client.Converters;

/// <summary>
/// Returns true if the member's role equals the parameter value.
/// Usage: IsVisible="{Binding Role, Converter={x:Static conv:RoleEqualsConverter.Instance}, ConverterParameter=Member}"
/// </summary>
public class RoleEqualsConverter : IValueConverter
{
    public static readonly RoleEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is UserRole role && parameter is string roleString)
        {
            return Enum.TryParse<UserRole>(roleString, out var targetRole) && role == targetRole;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns true if the member can be promoted (is currently a Member).
/// Owners and Admins cannot be promoted further.
/// </summary>
public class CanPromoteConverter : IValueConverter
{
    public static readonly CanPromoteConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is UserRole role)
        {
            // Can only promote Members to Admin
            return role == UserRole.Member;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns true if the member can be demoted (is currently an Admin).
/// Owners cannot be demoted, and Members are already at the lowest level.
/// </summary>
public class CanDemoteConverter : IValueConverter
{
    public static readonly CanDemoteConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is UserRole role)
        {
            // Can only demote Admins to Member
            // Owners cannot be demoted
            return role == UserRole.Admin;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns true if ownership can be transferred to this member.
/// Owners cannot transfer to themselves (they already own it).
/// </summary>
public class CanTransferOwnershipConverter : IValueConverter
{
    public static readonly CanTransferOwnershipConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is UserRole role)
        {
            // Can transfer ownership to Admins or Members, not to existing Owner
            return role != UserRole.Owner;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
