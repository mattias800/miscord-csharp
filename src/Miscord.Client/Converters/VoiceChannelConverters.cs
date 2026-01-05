using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Miscord.Client.Services;
using Miscord.Client.ViewModels;

namespace Miscord.Client.Converters;

/// <summary>
/// Compares a channel ID to the current voice channel ID.
/// Returns true if they match (channel has participants to show).
/// </summary>
public class IsCurrentVoiceChannelConverter : IMultiValueConverter
{
    public static readonly IsCurrentVoiceChannelConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return false;

        var channelId = values[0] as Guid?;
        var currentVoiceChannelId = values[1] as Guid?;

        return channelId.HasValue && currentVoiceChannelId.HasValue && channelId == currentVoiceChannelId;
    }
}

/// <summary>
/// Gets voice participants for a specific channel from the ViewModel.
/// Usage: MultiBinding with channel Id and ViewModel.
/// </summary>
public class ChannelVoiceParticipantsConverter : IMultiValueConverter
{
    public static readonly ChannelVoiceParticipantsConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return null;

        var channelId = values[0] as Guid?;
        var viewModel = values[1] as MainAppViewModel;

        if (channelId.HasValue && viewModel is not null)
        {
            return viewModel.GetChannelVoiceParticipants(channelId.Value);
        }

        return null;
    }
}

/// <summary>
/// Returns true if the channel has any voice participants.
/// </summary>
public class HasVoiceParticipantsConverter : IMultiValueConverter
{
    public static readonly HasVoiceParticipantsConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return false;

        var channelId = values[0] as Guid?;
        var viewModel = values[1] as MainAppViewModel;

        if (channelId.HasValue && viewModel is not null)
        {
            var participants = viewModel.GetChannelVoiceParticipants(channelId.Value);
            return participants.Count > 0;
        }

        return false;
    }
}

/// <summary>
/// Converts IsSpeaking boolean to foreground brush.
/// White when speaking, gray when not.
/// </summary>
public class SpeakingForegroundConverter : IValueConverter
{
    public static readonly SpeakingForegroundConverter Instance = new();

    private static readonly IBrush SpeakingBrush = new SolidColorBrush(Color.Parse("#ffffff"));
    private static readonly IBrush NotSpeakingBrush = new SolidColorBrush(Color.Parse("#8e9297"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SpeakingBrush : NotSpeakingBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
