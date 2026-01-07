using System.Globalization;
using Avalonia.Data.Converters;
using Miscord.Client.Services;

namespace Miscord.Client.Converters;

/// <summary>
/// Returns "selected" class if the conversation matches the selected conversation.
/// Parameters: [0] = Current conversation, [1] = Selected conversation
/// </summary>
public class IsSelectedConversationConverter : IMultiValueConverter
{
    public static readonly IsSelectedConversationConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return "conversation-btn";

        if (values[0] is not ConversationSummary current)
            return "conversation-btn";

        if (values[1] is not ConversationSummary selected)
            return "conversation-btn";

        return current.UserId == selected.UserId ? "conversation-btn selected" : "conversation-btn";
    }
}
