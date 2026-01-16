using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Snacka.Client.Controls;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;
using Snacka.Client.Views;

namespace Snacka.Client;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // HttpClient without a predefined base address - will be set dynamically
            // Timeout set to 20 seconds for better UX when server is slow/unreachable
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var apiClient = new ApiClient(httpClient);

            // Initialize MessageContentBlock with API client for link previews
            MessageContentBlock.SetApiClient(apiClient);

            var connectionStore = new ServerConnectionStore();
            var signalR = new SignalRService();
            var settingsStore = new SettingsStore(Program.Profile);
            var audioDeviceService = new AudioDeviceService(settingsStore);
            var videoDeviceService = new VideoDeviceService();
            var screenCaptureService = new ScreenCaptureService();
            var controllerService = new ControllerService();
            var controllerStreamingService = new ControllerStreamingService(signalR, controllerService, settingsStore);
            var controllerHostService = new ControllerHostService(signalR);
            var webRtc = new WebRtcService(signalR, settingsStore);

            // Check for dev mode auto-login
            DevLoginConfig? devConfig = null;
            if (!string.IsNullOrEmpty(Program.DevServerUrl) &&
                !string.IsNullOrEmpty(Program.DevEmail) &&
                !string.IsNullOrEmpty(Program.DevPassword))
            {
                devConfig = new DevLoginConfig(
                    Program.DevServerUrl,
                    Program.DevEmail,
                    Program.DevPassword
                );
            }

            var viewModel = new MainWindowViewModel(apiClient, connectionStore, signalR, webRtc, settingsStore, audioDeviceService, videoDeviceService, screenCaptureService, controllerService, controllerStreamingService, controllerHostService, devConfig: devConfig);

            var window = new MainWindow
            {
                DataContext = viewModel
            };

            // Set custom window title if provided
            if (!string.IsNullOrEmpty(Program.DevWindowTitle))
            {
                window.Title = Program.DevWindowTitle;
            }

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public record DevLoginConfig(string ServerUrl, string Email, string Password);
