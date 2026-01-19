using System.Diagnostics;
using System.Text.Json;
using Snacka.Client.Services.HardwareVideo;
using Snacka.Client.Services.WebRtc;

namespace Snacka.Client.Services;

/// <summary>
/// Service for detecting and reporting system capabilities at startup.
/// Outputs diagnostics to stdout for testing and debugging.
/// </summary>
public sealed class SystemCapabilityService : ISystemCapabilityService
{
    public bool IsHardwareEncoderAvailable { get; private set; }
    public string? HardwareEncoderName { get; private set; }
    public bool IsHardwareDecoderAvailable { get; private set; }
    public string? HardwareDecoderName { get; private set; }
    public bool IsNativeCaptureAvailable { get; private set; }
    public string? NativeCaptureName { get; private set; }
    public bool IsFullHardwareAccelerationAvailable =>
        IsHardwareEncoderAvailable && IsHardwareDecoderAvailable && IsNativeCaptureAvailable;

    private readonly List<string> _warnings = new();
    public IReadOnlyList<string> Warnings => _warnings;

    public CaptureValidationResult? ValidationResult { get; private set; }
    public bool HasValidationIssues => ValidationResult?.Issues.Any(i => i.IsError || i.IsWarning) == true;
    public bool IsValidationWarningDismissed { get; private set; }

    private const string ValidationDismissedKey = "validation_warning_dismissed";
    private const string ValidationResultHashKey = "validation_result_hash";

    /// <summary>
    /// Runs all capability checks and outputs results to stdout.
    /// Should be called at application startup.
    /// </summary>
    public void CheckCapabilities()
    {
        Console.WriteLine("");
        Console.WriteLine("=== System Capability Check ===");
        Console.WriteLine("");

        CheckNativeCapture();
        CheckHardwareEncoder();
        CheckHardwareDecoder();

        PrintSummary();
    }

    private void CheckNativeCapture()
    {
        Console.WriteLine("Checking native capture tools...");

        var locator = new NativeCaptureLocator();

        if (OperatingSystem.IsMacOS())
        {
            if (locator.ShouldUseSnackaCaptureVideoToolbox())
            {
                IsNativeCaptureAvailable = true;
                NativeCaptureName = "SnackaCaptureVideoToolbox";
                var path = locator.GetSnackaCaptureVideoToolboxPath();
                Console.WriteLine($"  [OK] Native capture: SnackaCaptureVideoToolbox");
                Console.WriteLine($"       Path: {path}");
            }
            else
            {
                IsNativeCaptureAvailable = false;
                Console.WriteLine("  [WARN] Native capture: SnackaCaptureVideoToolbox NOT FOUND");
                Console.WriteLine("         Screen share will use FFmpeg software path (higher CPU usage)");
                _warnings.Add("SnackaCaptureVideoToolbox not found. Screen sharing will use more CPU.");
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            if (locator.ShouldUseSnackaCaptureWindows())
            {
                IsNativeCaptureAvailable = true;
                NativeCaptureName = "SnackaCaptureWindows";
                var path = locator.GetSnackaCaptureWindowsPath();
                Console.WriteLine($"  [OK] Native capture: SnackaCaptureWindows");
                Console.WriteLine($"       Path: {path}");
            }
            else
            {
                IsNativeCaptureAvailable = false;
                Console.WriteLine("  [WARN] Native capture: SnackaCaptureWindows NOT FOUND");
                Console.WriteLine("         Screen share will use FFmpeg software path (higher CPU usage)");
                _warnings.Add("SnackaCaptureWindows not found. Screen sharing will use more CPU.");
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            if (locator.ShouldUseSnackaCaptureLinux())
            {
                IsNativeCaptureAvailable = true;
                NativeCaptureName = "SnackaCaptureLinux";
                var path = locator.GetSnackaCaptureLinuxPath();
                Console.WriteLine($"  [OK] Native capture: SnackaCaptureLinux");
                Console.WriteLine($"       Path: {path}");
            }
            else
            {
                IsNativeCaptureAvailable = false;
                Console.WriteLine("  [WARN] Native capture: SnackaCaptureLinux NOT FOUND");
                Console.WriteLine("         Screen share will use FFmpeg software path (higher CPU usage)");
                _warnings.Add("SnackaCaptureLinux not found. Screen sharing will use more CPU.");
            }
        }
        else
        {
            Console.WriteLine("  [WARN] Unsupported platform for native capture");
            _warnings.Add("Unsupported platform for hardware-accelerated capture.");
        }

        Console.WriteLine("");
    }

    private void CheckHardwareEncoder()
    {
        Console.WriteLine("Checking hardware video encoders...");

        // If native capture is available, encoding is handled by the native tool
        // (Media Foundation on Windows, VideoToolbox on macOS, VAAPI on Linux)
        // We don't need to check ffmpeg encoders in that case
        if (IsNativeCaptureAvailable)
        {
            if (OperatingSystem.IsMacOS())
            {
                IsHardwareEncoderAvailable = true;
                HardwareEncoderName = "VideoToolbox (via native capture)";
                Console.WriteLine("  [OK] Hardware encoder: VideoToolbox (via SnackaCaptureVideoToolbox)");
            }
            else if (OperatingSystem.IsWindows())
            {
                // SnackaCaptureWindows uses Media Foundation which auto-selects best encoder
                IsHardwareEncoderAvailable = true;
                HardwareEncoderName = "Media Foundation (via native capture)";
                Console.WriteLine("  [OK] Hardware encoder: Media Foundation (via SnackaCaptureWindows)");
                Console.WriteLine("       Media Foundation will use NVENC/AMF/QuickSync if available");
            }
            else if (OperatingSystem.IsLinux())
            {
                IsHardwareEncoderAvailable = true;
                HardwareEncoderName = "VAAPI (via native capture)";
                Console.WriteLine("  [OK] Hardware encoder: VAAPI (via SnackaCaptureLinux)");
            }
            Console.WriteLine("");
            return;
        }

        // Native capture not available - check ffmpeg hardware encoders for fallback path
        Console.WriteLine("  Native capture unavailable, checking ffmpeg hardware encoders...");

        if (OperatingSystem.IsMacOS())
        {
            // VideoToolbox is always available on macOS via ffmpeg
            IsHardwareEncoderAvailable = true;
            HardwareEncoderName = "VideoToolbox (ffmpeg)";
            Console.WriteLine("  [OK] Hardware encoder: VideoToolbox (via ffmpeg)");
        }
        else if (OperatingSystem.IsWindows())
        {
            CheckWindowsFfmpegEncoders();
        }
        else if (OperatingSystem.IsLinux())
        {
            CheckLinuxFfmpegEncoders();
        }
        else
        {
            Console.WriteLine("  [WARN] Unsupported platform for hardware encoding");
            _warnings.Add("Unsupported platform for hardware-accelerated encoding.");
        }

        if (!IsHardwareEncoderAvailable)
        {
            Console.WriteLine("  [WARN] No hardware encoder found - will use libx264 (CPU-only, high usage)");
            _warnings.Add("No hardware video encoder available. Screen sharing will use software encoding (high CPU usage).");
        }

        Console.WriteLine("");
    }

    private void CheckWindowsFfmpegEncoders()
    {
        // Check ffmpeg hardware encoders in order of preference: NVENC, AMF, QuickSync
        var encoders = new[]
        {
            ("h264_nvenc", "NVENC", "NVIDIA GPU"),
            ("h264_amf", "AMF", "AMD GPU"),
            ("h264_qsv", "QuickSync", "Intel GPU"),
        };

        foreach (var (encoder, name, description) in encoders)
        {
            Console.WriteLine($"  Checking ffmpeg {name} ({description})...");
            if (IsEncoderAvailable(encoder))
            {
                IsHardwareEncoderAvailable = true;
                HardwareEncoderName = $"{name} (ffmpeg)";
                Console.WriteLine($"  [OK] Hardware encoder: {name} via ffmpeg ({description})");
                return;
            }
            else
            {
                Console.WriteLine($"       {name} not available in ffmpeg");
            }
        }
    }

    private void CheckLinuxFfmpegEncoders()
    {
        Console.WriteLine("  Checking ffmpeg VAAPI...");
        if (IsEncoderAvailable("h264_vaapi"))
        {
            IsHardwareEncoderAvailable = true;
            HardwareEncoderName = "VAAPI (ffmpeg)";
            Console.WriteLine("  [OK] Hardware encoder: VAAPI via ffmpeg (Intel/AMD GPU)");
        }
        else
        {
            Console.WriteLine("       VAAPI not available in ffmpeg");
        }
    }

    private void CheckHardwareDecoder()
    {
        Console.WriteLine("Checking hardware video decoders...");

        try
        {
            var isAvailable = HardwareVideoDecoderFactory.IsAvailable();

            if (isAvailable)
            {
                IsHardwareDecoderAvailable = true;

                if (OperatingSystem.IsMacOS())
                {
                    HardwareDecoderName = "VideoToolbox";
                    Console.WriteLine("  [OK] Hardware decoder: VideoToolbox");
                }
                else if (OperatingSystem.IsWindows())
                {
                    HardwareDecoderName = "MediaFoundation";
                    Console.WriteLine("  [OK] Hardware decoder: Media Foundation (D3D11)");
                }
                else if (OperatingSystem.IsLinux())
                {
                    HardwareDecoderName = "VAAPI";
                    Console.WriteLine("  [OK] Hardware decoder: VAAPI");
                }

                // Verify decoder can be created
                var decoder = HardwareVideoDecoderFactory.Create();
                if (decoder != null)
                {
                    Console.WriteLine("       Decoder instance created successfully");
                    decoder.Dispose();
                }
                else
                {
                    Console.WriteLine("  [WARN] Decoder available but instance creation failed");
                    _warnings.Add("Hardware decoder detection succeeded but instance creation failed.");
                }
            }
            else
            {
                IsHardwareDecoderAvailable = false;
                Console.WriteLine("  [WARN] No hardware decoder available");
                Console.WriteLine("         Video playback will use software decoding (higher CPU usage)");
                _warnings.Add("No hardware video decoder available. Video playback will use more CPU.");
            }
        }
        catch (Exception ex)
        {
            IsHardwareDecoderAvailable = false;
            Console.WriteLine($"  [ERROR] Hardware decoder check failed: {ex.Message}");
            _warnings.Add($"Hardware decoder check failed: {ex.Message}");
        }

        Console.WriteLine("");
    }

    private void PrintSummary()
    {
        Console.WriteLine("=== Capability Summary ===");
        Console.WriteLine("");
        Console.WriteLine($"  Native Capture:    {(IsNativeCaptureAvailable ? $"[OK] {NativeCaptureName}" : "[MISSING]")}");
        Console.WriteLine($"  Hardware Encoder:  {(IsHardwareEncoderAvailable ? $"[OK] {HardwareEncoderName}" : "[MISSING] (using libx264)")}");
        Console.WriteLine($"  Hardware Decoder:  {(IsHardwareDecoderAvailable ? $"[OK] {HardwareDecoderName}" : "[MISSING]")}");
        Console.WriteLine("");

        if (IsFullHardwareAccelerationAvailable)
        {
            Console.WriteLine("  Status: Full hardware acceleration available");
        }
        else
        {
            Console.WriteLine("  Status: REDUCED PERFORMANCE - Some hardware acceleration unavailable");
            Console.WriteLine("");
            Console.WriteLine("  Warnings:");
            foreach (var warning in _warnings)
            {
                Console.WriteLine($"    - {warning}");
            }
        }

        Console.WriteLine("");
        Console.WriteLine("=== End Capability Check ===");
        Console.WriteLine("");
    }

    private static string? _ffmpegPath;

    private static string GetFfmpegPath()
    {
        if (_ffmpegPath != null) return _ffmpegPath;

        if (OperatingSystem.IsMacOS())
        {
            var paths = new[]
            {
                "/opt/homebrew/bin/ffmpeg",
                "/opt/homebrew/opt/ffmpeg@6/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/usr/local/opt/ffmpeg@6/bin/ffmpeg",
                "/usr/bin/ffmpeg"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    _ffmpegPath = path;
                    return _ffmpegPath;
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var paths = new[] { "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg" };
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    _ffmpegPath = path;
                    return _ffmpegPath;
                }
            }
        }

        _ffmpegPath = "ffmpeg";
        return _ffmpegPath;
    }

    private static bool IsEncoderAvailable(string encoder)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = GetFfmpegPath(),
                Arguments = "-hide_banner -encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            return output.Contains(encoder);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs detailed validation using the native capture tool.
    /// Currently only implemented for Linux.
    /// </summary>
    public async Task ValidateAsync()
    {
        // Only run validation on Linux for now
        if (!OperatingSystem.IsLinux())
        {
            Console.WriteLine("SystemCapabilityService: Skipping validation (not Linux)");
            return;
        }

        var locator = new NativeCaptureLocator();
        var capturePath = locator.GetSnackaCaptureLinuxPath();

        if (capturePath == null)
        {
            Console.WriteLine("SystemCapabilityService: Cannot validate - SnackaCaptureLinux not found");
            ValidationResult = new CaptureValidationResult
            {
                Platform = "linux",
                CanCapture = false,
                CanEncodeH264 = false,
                Issues = new List<CaptureValidationIssue>
                {
                    new()
                    {
                        Severity = "error",
                        Code = "NATIVE_TOOL_MISSING",
                        Title = "Native capture tool not found",
                        Description = "SnackaCaptureLinux is not available. Screen sharing and camera features will not work.",
                        Suggestions = new List<string>
                        {
                            "Ensure SnackaCaptureLinux is included in the application directory",
                            "Try reinstalling the application"
                        }
                    }
                }
            };
            return;
        }

        try
        {
            Console.WriteLine($"SystemCapabilityService: Running validation with {capturePath}");

            var psi = new ProcessStartInfo
            {
                FileName = capturePath,
                Arguments = "validate --json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.WriteLine("SystemCapabilityService: Failed to start validation process");
                return;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(stderr))
            {
                Console.WriteLine($"SystemCapabilityService: Validation stderr: {stderr}");
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine("SystemCapabilityService: Validation returned no output");
                return;
            }

            ValidationResult = JsonSerializer.Deserialize<CaptureValidationResult>(output);

            if (ValidationResult != null)
            {
                Console.WriteLine($"SystemCapabilityService: Validation complete");
                Console.WriteLine($"  GPU Vendor: {ValidationResult.GpuVendor}");
                Console.WriteLine($"  Can Encode H.264: {ValidationResult.CanEncodeH264}");
                Console.WriteLine($"  Issues: {ValidationResult.Issues.Count}");

                foreach (var issue in ValidationResult.Issues)
                {
                    Console.WriteLine($"    [{issue.Severity.ToUpper()}] {issue.Code}: {issue.Title}");
                }

                // Check if dismissal is still valid (based on hash of result)
                LoadDismissalState();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SystemCapabilityService: Validation failed - {ex.Message}");
        }
    }

    /// <summary>
    /// Dismisses the validation warning banner. The dismissal is persisted.
    /// </summary>
    public void DismissValidationWarning()
    {
        IsValidationWarningDismissed = true;

        // Persist dismissal with a hash of the current result
        // so it reappears if the validation result changes
        try
        {
            var hash = ComputeValidationHash();
            var settingsPath = GetSettingsFilePath();

            var settings = LoadSettings(settingsPath);
            settings[ValidationDismissedKey] = "true";
            settings[ValidationResultHashKey] = hash;
            SaveSettings(settingsPath, settings);

            Console.WriteLine("SystemCapabilityService: Validation warning dismissed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SystemCapabilityService: Failed to persist dismissal - {ex.Message}");
        }
    }

    private void LoadDismissalState()
    {
        try
        {
            var settingsPath = GetSettingsFilePath();
            if (!File.Exists(settingsPath))
            {
                IsValidationWarningDismissed = false;
                return;
            }

            var settings = LoadSettings(settingsPath);

            if (settings.TryGetValue(ValidationDismissedKey, out var dismissed) &&
                dismissed == "true")
            {
                // Check if the hash matches
                if (settings.TryGetValue(ValidationResultHashKey, out var savedHash))
                {
                    var currentHash = ComputeValidationHash();
                    IsValidationWarningDismissed = savedHash == currentHash;

                    if (!IsValidationWarningDismissed)
                    {
                        Console.WriteLine("SystemCapabilityService: Validation result changed, showing warning again");
                    }
                }
                else
                {
                    IsValidationWarningDismissed = true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SystemCapabilityService: Failed to load dismissal state - {ex.Message}");
            IsValidationWarningDismissed = false;
        }
    }

    private string ComputeValidationHash()
    {
        if (ValidationResult == null) return "";

        // Simple hash based on key validation properties
        var key = $"{ValidationResult.GpuVendor}|{ValidationResult.CanEncodeH264}|{ValidationResult.Issues.Count}";
        return key.GetHashCode().ToString("X8");
    }

    private static string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var snackaDir = Path.Combine(appData, "Snacka");
        Directory.CreateDirectory(snackaDir);
        return Path.Combine(snackaDir, "capability_settings.json");
    }

    private static Dictionary<string, string> LoadSettings(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    private static void SaveSettings(string path, Dictionary<string, string> settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
