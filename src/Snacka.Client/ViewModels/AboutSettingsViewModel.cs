using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using Snacka.Client.Services;

namespace Snacka.Client.ViewModels;

public class AboutSettingsViewModel : ViewModelBase
{
    private string _copyStatus = "";

    public AboutSettingsViewModel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version ?? new Version(0, 1, 0);

        Version = $"{version.Major}.{version.Minor}.{version.Build}";
        FullVersion = version.ToString();
        DotNetVersion = RuntimeInformation.FrameworkDescription;
        OperatingSystem = RuntimeInformation.OSDescription;
        Architecture = RuntimeInformation.OSArchitecture.ToString();
        RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier;

        // Log-related properties
        try
        {
            LogFilePath = LogService.Instance.LogFilePath;
            LogDirectory = LogService.Instance.LogDirectory;
        }
        catch
        {
            LogFilePath = "Logging not initialized";
            LogDirectory = "";
        }

        CopyLogsCommand = ReactiveCommand.CreateFromTask(CopyLogsToClipboardAsync);
        OpenLogsFolderCommand = ReactiveCommand.Create(OpenLogsFolder);
    }

    public string Version { get; }
    public string FullVersion { get; }
    public string DotNetVersion { get; }
    public string OperatingSystem { get; }
    public string Architecture { get; }
    public string RuntimeIdentifier { get; }

    // Log-related properties
    public string LogFilePath { get; }
    public string LogDirectory { get; }

    public string CopyStatus
    {
        get => _copyStatus;
        set => this.RaiseAndSetIfChanged(ref _copyStatus, value);
    }

    public ICommand CopyLogsCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }

    private async Task CopyLogsToClipboardAsync()
    {
        try
        {
            var logs = LogService.Instance.GetLogs();

            // Add system info header for bug reports
            var header = $"""
                === Snacka Bug Report ===
                Version: {FullVersion}
                .NET: {DotNetVersion}
                OS: {OperatingSystem}
                Architecture: {Architecture}
                Runtime: {RuntimeIdentifier}
                Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

                === Logs ===

                """;

            var fullReport = header + logs;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(fullReport);
                    CopyStatus = "Copied to clipboard!";

                    // Clear status after 3 seconds
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => CopyStatus = "");
                    });
                }
            }
        }
        catch (Exception ex)
        {
            CopyStatus = $"Error: {ex.Message}";
        }
    }

    private void OpenLogsFolder()
    {
        try
        {
            if (string.IsNullOrEmpty(LogDirectory) || !Directory.Exists(LogDirectory))
            {
                CopyStatus = "Log folder not found";
                return;
            }

            // Open folder in file manager
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", LogDirectory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", LogDirectory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", LogDirectory);
            }
        }
        catch (Exception ex)
        {
            CopyStatus = $"Error: {ex.Message}";
        }
    }
}
