using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Data.Converters;
using Snacka.Client.Services;

namespace Snacka.Client.Converters;

/// <summary>
/// Checks if a message is at the first unread index in the messages list.
/// Parameters: [0] = DirectMessageResponse, [1] = FirstUnreadIndex, [2] = Messages collection
/// </summary>
public class IsFirstUnreadMessageConverter : IMultiValueConverter
{
    public static readonly IsFirstUnreadMessageConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3)
            return false;

        if (values[0] is not DirectMessageResponse message)
            return false;

        if (values[1] is not int firstUnreadIndex || firstUnreadIndex < 0)
            return false;

        if (values[2] is not ObservableCollection<DirectMessageResponse> messages)
            return false;

        var messageIndex = messages.IndexOf(message);
        return messageIndex == firstUnreadIndex;
    }
}
