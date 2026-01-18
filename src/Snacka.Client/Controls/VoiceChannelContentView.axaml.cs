using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;

namespace Snacka.Client.Controls;

/// <summary>
/// Voice channel content area showing header, warning banner, and video streams grid.
/// </summary>
public partial class VoiceChannelContentView : UserControl
{
    public static readonly StyledProperty<ChannelResponse?> SelectedVoiceChannelProperty =
        AvaloniaProperty.Register<VoiceChannelContentView, ChannelResponse?>(nameof(SelectedVoiceChannel));

    public static readonly StyledProperty<bool> ShowAudioDeviceWarningProperty =
        AvaloniaProperty.Register<VoiceChannelContentView, bool>(nameof(ShowAudioDeviceWarning));

    public static readonly StyledProperty<VoiceChannelContentViewModel?> VoiceChannelContentProperty =
        AvaloniaProperty.Register<VoiceChannelContentView, VoiceChannelContentViewModel?>(nameof(VoiceChannelContent));

    public static readonly StyledProperty<ICommand?> OpenSettingsCommandProperty =
        AvaloniaProperty.Register<VoiceChannelContentView, ICommand?>(nameof(OpenSettingsCommand));

    public VoiceChannelContentView()
    {
        InitializeComponent();
    }

    public ChannelResponse? SelectedVoiceChannel
    {
        get => GetValue(SelectedVoiceChannelProperty);
        set => SetValue(SelectedVoiceChannelProperty, value);
    }

    public bool ShowAudioDeviceWarning
    {
        get => GetValue(ShowAudioDeviceWarningProperty);
        set => SetValue(ShowAudioDeviceWarningProperty, value);
    }

    public VoiceChannelContentViewModel? VoiceChannelContent
    {
        get => GetValue(VoiceChannelContentProperty);
        set => SetValue(VoiceChannelContentProperty, value);
    }

    public ICommand? OpenSettingsCommand
    {
        get => GetValue(OpenSettingsCommandProperty);
        set => SetValue(OpenSettingsCommandProperty, value);
    }

    // Events for video stream interactions
    public event EventHandler<VideoStreamViewModel>? WatchScreenShareRequested;
    public event EventHandler<VideoStreamViewModel>? StopWatchingRequested;
    public event EventHandler<VideoStreamViewModel>? FullscreenRequested;
    public event EventHandler<VideoStreamViewModel>? VideoTileDoubleTapped;
    public event EventHandler<VideoStreamViewModel>? ShareControllerRequested;

    private void OnWatchScreenShareClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VideoStreamViewModel stream)
        {
            WatchScreenShareRequested?.Invoke(this, stream);
        }
    }

    private void OnStopWatchingClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VideoStreamViewModel stream)
        {
            StopWatchingRequested?.Invoke(this, stream);
        }
    }

    private void OnFullscreenButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VideoStreamViewModel stream)
        {
            FullscreenRequested?.Invoke(this, stream);
        }
    }

    private void OnVideoTileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Tag is VideoStreamViewModel stream)
        {
            VideoTileDoubleTapped?.Invoke(this, stream);
        }
    }

    private void OnShareControllerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VideoStreamViewModel stream)
        {
            ShareControllerRequested?.Invoke(this, stream);
        }
    }

    private void OnGamingStationShareScreenClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VideoStreamViewModel stream)
        {
            // Invoke the gaming station share screen callback if available
            if (stream.GamingStationMachineId is not null)
            {
                stream.OnShareScreenCommand?.Invoke(stream.GamingStationMachineId);
            }
        }
    }
}
