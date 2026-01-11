using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace Snacka.Client.Services;

/// <summary>
/// Information about an available update.
/// </summary>
public record UpdateInfo(
    string Version,
    string? ReleaseNotes,
    bool IsDownloaded
);

/// <summary>
/// Update state for UI binding.
/// </summary>
public enum UpdateState
{
    NoUpdate,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    Error
}

/// <summary>
/// Service for checking and applying updates using Velopack.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Gets the current application version.
    /// </summary>
    Version CurrentVersion { get; }

    /// <summary>
    /// Gets whether the app was installed via Velopack (vs running from source/dev).
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Checks for updates and returns info if a newer version is available.
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync();

    /// <summary>
    /// Downloads the update in the background.
    /// </summary>
    Task DownloadUpdateAsync(Action<int>? progressCallback = null);

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// </summary>
    void ApplyUpdateAndRestart();

    /// <summary>
    /// Opens the releases page in the default browser.
    /// </summary>
    void OpenReleasesPage();
}

public class UpdateService : IUpdateService
{
    private readonly UpdateManager? _updateManager;
    private UpdateInfo? _cachedUpdate;
    private const string GitHubRepoUrl = "https://github.com/mattias800/snacka";
    private const string GitHubReleasesUrl = "https://github.com/mattias800/snacka/releases";

    public UpdateService()
    {
        try
        {
            // GithubSource: repoUrl, accessToken (null for public), prerelease
            var source = new GithubSource(GitHubRepoUrl, null, false);
            _updateManager = new UpdateManager(source);
            Console.WriteLine($"UpdateService: Initialized with Velopack. IsInstalled: {IsInstalled}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateService: Failed to initialize Velopack: {ex.Message}");
            _updateManager = null;
        }
    }

    public Version CurrentVersion
    {
        get
        {
            // Try to get version from Velopack first
            if (_updateManager?.IsInstalled == true)
            {
                try
                {
                    var veloVersion = _updateManager.CurrentVersion;
                    if (veloVersion != null)
                    {
                        return new Version(veloVersion.Major, veloVersion.Minor, veloVersion.Patch);
                    }
                }
                catch
                {
                    // Fall through to assembly version
                }
            }

            // Fallback to assembly version
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version ?? new Version(0, 1, 0);
        }
    }

    public bool IsInstalled => _updateManager?.IsInstalled ?? false;

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        if (_updateManager == null)
        {
            Console.WriteLine("UpdateService: UpdateManager not available");
            return null;
        }

        if (!IsInstalled)
        {
            Console.WriteLine("UpdateService: App not installed via Velopack, skipping update check");
            return null;
        }

        try
        {
            Console.WriteLine($"UpdateService: Checking for updates (current version: {CurrentVersion})");

            var updateInfo = await _updateManager.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                Console.WriteLine("UpdateService: No updates available");
                _cachedUpdate = null;
                return null;
            }

            var newVersion = updateInfo.TargetFullRelease.Version;
            Console.WriteLine($"UpdateService: Update available: {newVersion}");

            _cachedUpdate = new UpdateInfo(
                Version: newVersion.ToString(),
                ReleaseNotes: null, // Velopack doesn't provide release notes directly
                IsDownloaded: false
            );

            return _cachedUpdate;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateService: Error checking for updates: {ex.Message}");
            return null;
        }
    }

    public async Task DownloadUpdateAsync(Action<int>? progressCallback = null)
    {
        if (_updateManager == null || !IsInstalled)
        {
            Console.WriteLine("UpdateService: Cannot download - not installed or manager unavailable");
            return;
        }

        try
        {
            Console.WriteLine("UpdateService: Starting update download...");

            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                Console.WriteLine("UpdateService: No update to download");
                return;
            }

            await _updateManager.DownloadUpdatesAsync(
                updateInfo,
                progress => progressCallback?.Invoke(progress)
            );

            Console.WriteLine("UpdateService: Download complete");

            // Update cached info to mark as downloaded
            if (_cachedUpdate != null)
            {
                _cachedUpdate = _cachedUpdate with { IsDownloaded = true };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateService: Error downloading update: {ex.Message}");
            throw;
        }
    }

    public void ApplyUpdateAndRestart()
    {
        if (_updateManager == null || !IsInstalled)
        {
            Console.WriteLine("UpdateService: Cannot apply update - not installed or manager unavailable");
            return;
        }

        try
        {
            Console.WriteLine("UpdateService: Applying update and restarting...");
            _updateManager.ApplyUpdatesAndRestart(null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateService: Error applying update: {ex.Message}");
            throw;
        }
    }

    public void OpenReleasesPage()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = GitHubReleasesUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateService: Failed to open URL: {ex.Message}");
        }
    }
}
