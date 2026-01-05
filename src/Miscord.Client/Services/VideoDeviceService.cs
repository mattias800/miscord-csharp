using System.Reflection;
using System.Runtime.InteropServices;
using SIPSorceryMedia.FFmpeg;
using SIPSorceryMedia.Abstractions;
using FFmpeg.AutoGen;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;

namespace Miscord.Client.Services;

public interface IVideoDeviceService : IDisposable
{
    IReadOnlyList<VideoDeviceInfo> GetCameraDevices();

    bool IsTestingCamera { get; }

    Task StartCameraTestAsync(string? devicePath, Action<byte[], int, int> onFrameReceived);
    Task StopTestAsync();
}

public record VideoDeviceInfo(string Path, string Name);

public class VideoDeviceService : IVideoDeviceService
{
    private static bool _ffmpegInitialized;
    private static readonly object _ffmpegInitLock = new();

    private readonly ISettingsStore? _settingsStore;
    private FFmpegCameraSource? _testCameraSource;
    private Action<byte[], int, int>? _onFrameReceived;
    private CancellationTokenSource? _testCts;

    public bool IsTestingCamera => _testCameraSource != null;

    // FFmpeg library paths to try on macOS
    private static readonly string[] FFmpegLibPaths =
    {
        "/opt/homebrew/lib",      // Apple Silicon Homebrew
        "/usr/local/lib",         // Intel Homebrew
        "/usr/lib",               // System
    };

    public VideoDeviceService(ISettingsStore? settingsStore = null)
    {
        _settingsStore = settingsStore;
        EnsureFFmpegInitialized();
    }

    private static IntPtr ResolveFFmpegLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // FFmpeg libraries: avcodec, avformat, avutil, avdevice, swscale, swresample
        var ffmpegLibs = new[] { "avcodec", "avformat", "avutil", "avdevice", "swscale", "swresample", "avfilter" };

        foreach (var libName in ffmpegLibs)
        {
            if (libraryName.Contains(libName))
            {
                foreach (var basePath in FFmpegLibPaths)
                {
                    // Try versioned library names (macOS uses .dylib)
                    var paths = new[]
                    {
                        Path.Combine(basePath, $"lib{libName}.dylib"),
                        Path.Combine(basePath, $"lib{libName}.so"),
                    };

                    foreach (var path in paths)
                    {
                        if (NativeLibrary.TryLoad(path, out var handle))
                        {
                            Console.WriteLine($"VideoDeviceService: Loaded {libraryName} from {path}");
                            return handle;
                        }
                    }
                }
            }
        }

        return IntPtr.Zero;
    }

    private static void EnsureFFmpegInitialized()
    {
        if (_ffmpegInitialized) return;

        lock (_ffmpegInitLock)
        {
            if (_ffmpegInitialized) return;

            try
            {
                // Find the FFmpeg library path
                string? ffmpegPath = null;
                foreach (var basePath in FFmpegLibPaths)
                {
                    if (File.Exists(Path.Combine(basePath, "libavcodec.dylib")) ||
                        File.Exists(Path.Combine(basePath, "libavcodec.so")))
                    {
                        ffmpegPath = basePath;
                        break;
                    }
                }

                if (ffmpegPath != null)
                {
                    Console.WriteLine($"VideoDeviceService: Found FFmpeg libraries in {ffmpegPath}");

                    // Set FFmpeg.AutoGen paths - both RootPath and LibrariesPath
                    ffmpeg.RootPath = ffmpegPath;
                    FFmpeg.AutoGen.Bindings.DynamicallyLoaded.DynamicallyLoadedBindings.LibrariesPath = ffmpegPath;
                    Console.WriteLine($"VideoDeviceService: Set FFmpeg library paths to {ffmpegPath}");

                    // Register DllImportResolver as backup
                    var ffmpegAutoGenAssembly = typeof(ffmpeg).Assembly;
                    NativeLibrary.SetDllImportResolver(ffmpegAutoGenAssembly, ResolveFFmpegLibrary);
                    Console.WriteLine($"VideoDeviceService: Registered DllImportResolver for {ffmpegAutoGenAssembly.GetName().Name}");

                    // Initialize FFmpeg.AutoGen bindings directly
                    try
                    {
                        FFmpeg.AutoGen.Bindings.DynamicallyLoaded.DynamicallyLoadedBindings.Initialize();
                        Console.WriteLine("VideoDeviceService: FFmpeg.AutoGen bindings initialized");
                    }
                    catch (KeyNotFoundException knfe) when (knfe.Message.Contains("postproc"))
                    {
                        Console.WriteLine("VideoDeviceService: postproc library not found, trying without it...");
                    }

                    // Try FFmpegInit for SIPSorceryMedia.FFmpeg integration
                    try
                    {
                        FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_WARNING, ffmpegPath);
                        Console.WriteLine("VideoDeviceService: FFmpeg initialized successfully via FFmpegInit");
                    }
                    catch (KeyNotFoundException knfe) when (knfe.Message.Contains("postproc"))
                    {
                        Console.WriteLine("VideoDeviceService: postproc library not found (expected on macOS Homebrew)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"VideoDeviceService: FFmpegInit failed - {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("VideoDeviceService: FFmpeg libraries not found in standard paths, trying default...");
                    FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_WARNING);
                }

                _ffmpegInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoDeviceService: Failed to initialize FFmpeg - {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"  Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                Console.WriteLine("  Make sure FFmpeg is installed:");
                Console.WriteLine("    macOS: brew install ffmpeg");
                Console.WriteLine("    Windows: winget install ffmpeg");
                Console.WriteLine("    Linux: apt install ffmpeg");
                _ffmpegInitialized = true; // Mark as attempted even if failed
            }
        }
    }

    public IReadOnlyList<VideoDeviceInfo> GetCameraDevices()
    {
        Console.WriteLine("VideoDeviceService: Getting camera devices...");

        // Try command-line ffmpeg first (more reliable on macOS)
        var cmdLineDevices = GetCameraDevicesViaCommandLine();
        if (cmdLineDevices.Count > 0)
        {
            return cmdLineDevices;
        }

        // Fall back to FFmpegCameraManager
        try
        {
            EnsureFFmpegInitialized();

            var devices = FFmpegCameraManager.GetCameraDevices();
            if (devices == null)
            {
                return Array.Empty<VideoDeviceInfo>();
            }
            var result = devices.Select(d => new VideoDeviceInfo(d.Path, d.Name)).ToList();

            Console.WriteLine($"VideoDeviceService: Found {result.Count} camera devices via FFmpegCameraManager");
            foreach (var device in result)
            {
                Console.WriteLine($"  - Camera: {device.Name} ({device.Path})");
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoDeviceService: Failed to get camera devices via FFmpegCameraManager - {ex.GetType().Name}: {ex.Message}");
            return Array.Empty<VideoDeviceInfo>();
        }
    }

    private IReadOnlyList<VideoDeviceInfo> GetCameraDevicesViaCommandLine()
    {
        try
        {
            // On macOS, use ffmpeg to list avfoundation devices
            if (!OperatingSystem.IsMacOS())
            {
                return Array.Empty<VideoDeviceInfo>();
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-f avfoundation -list_devices true -i \"\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                return Array.Empty<VideoDeviceInfo>();
            }

            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            // Parse the output to find video devices
            // Format: [AVFoundation indev @ 0x...] [0] FaceTime HD Camera
            var devices = new List<VideoDeviceInfo>();
            var lines = stderr.Split('\n');
            var inVideoDevices = false;

            foreach (var line in lines)
            {
                if (line.Contains("AVFoundation video devices:"))
                {
                    inVideoDevices = true;
                    continue;
                }
                if (line.Contains("AVFoundation audio devices:"))
                {
                    inVideoDevices = false;
                    continue;
                }

                if (inVideoDevices && line.Contains("[") && line.Contains("]"))
                {
                    // Parse line like: [AVFoundation indev @ 0x...] [0] FaceTime HD Camera
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d+)\]\s+(.+)$");
                    if (match.Success)
                    {
                        var index = match.Groups[1].Value;
                        var name = match.Groups[2].Value.Trim();
                        devices.Add(new VideoDeviceInfo(index, name));
                    }
                }
            }

            Console.WriteLine($"VideoDeviceService: Found {devices.Count} camera devices via ffmpeg command line");
            foreach (var device in devices)
            {
                Console.WriteLine($"  - Camera: {device.Name} (index {device.Path})");
            }

            return devices;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoDeviceService: Failed to enumerate via command line - {ex.Message}");
            return Array.Empty<VideoDeviceInfo>();
        }
    }

    public async Task StartCameraTestAsync(string? devicePath, Action<byte[], int, int> onFrameReceived)
    {
        await StopTestAsync();

        _onFrameReceived = onFrameReceived;
        _testCts = new CancellationTokenSource();

        // If no device path specified, use device 0
        if (string.IsNullOrEmpty(devicePath))
        {
            devicePath = "0";
        }

        Console.WriteLine($"VideoDeviceService: Starting camera test with device: {devicePath}");

        // Use command-line ffmpeg for camera capture on macOS (more reliable)
        if (OperatingSystem.IsMacOS())
        {
            await StartCameraTestViaCommandLineAsync(devicePath);
            return;
        }

        // Fall back to FFmpegCameraSource on other platforms
        try
        {
            EnsureFFmpegInitialized();

            Console.WriteLine($"VideoDeviceService: Creating FFmpegCameraSource for device: {devicePath}");
            _testCameraSource = new FFmpegCameraSource(devicePath);

            _testCameraSource.OnVideoSourceRawSample += OnVideoFrame;
            _testCameraSource.OnVideoSourceError += (error) =>
            {
                Console.WriteLine($"VideoDeviceService: Camera error: {error}");
            };

            Console.WriteLine("VideoDeviceService: Starting video capture...");
            await _testCameraSource.StartVideo();
            Console.WriteLine($"VideoDeviceService: Started camera test on device: {devicePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoDeviceService: Failed to start camera test - {ex.GetType().Name}: {ex.Message}");
            await StopTestAsync();
            throw;
        }
    }

    private System.Diagnostics.Process? _ffmpegProcess;
    private const int PreviewWidth = 640;
    private const int PreviewHeight = 360; // 16:9 aspect ratio

    private async Task StartCameraTestViaCommandLineAsync(string deviceIndex)
    {
        Console.WriteLine($"VideoDeviceService: Starting ffmpeg capture for device {deviceIndex}");

        // Use ffmpeg to capture from avfoundation - let it use native resolution and scale down for preview
        // No -video_size means use device default, then scale to preview size
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            // Capture at native resolution, scale to preview size, output raw RGB24
            Arguments = $"-f avfoundation -framerate 15 -i \"{deviceIndex}:none\" -vf \"scale={PreviewWidth}:{PreviewHeight}\" -f rawvideo -pix_fmt rgb24 -",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _ffmpegProcess = System.Diagnostics.Process.Start(psi);
        if (_ffmpegProcess == null)
        {
            throw new InvalidOperationException("Failed to start ffmpeg process");
        }

        Console.WriteLine("VideoDeviceService: ffmpeg process started, reading frames...");

        // Read frames in background
        var token = _testCts!.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var stream = _ffmpegProcess.StandardOutput.BaseStream;
                var frameSize = PreviewWidth * PreviewHeight * 3; // RGB24
                var buffer = new byte[frameSize];

                while (!token.IsCancellationRequested && !_ffmpegProcess.HasExited)
                {
                    var bytesRead = 0;
                    while (bytesRead < frameSize && !token.IsCancellationRequested)
                    {
                        var read = await stream.ReadAsync(buffer, bytesRead, frameSize - bytesRead, token);
                        if (read == 0) break;
                        bytesRead += read;
                    }

                    if (bytesRead == frameSize)
                    {
                        _onFrameReceived?.Invoke(buffer, PreviewWidth, PreviewHeight);
                    }
                    else if (bytesRead > 0)
                    {
                        Console.WriteLine($"VideoDeviceService: Incomplete frame ({bytesRead}/{frameSize} bytes)");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoDeviceService: Error reading frames - {ex.Message}");
            }
        }, token);

        // Log stderr in background
        _ = Task.Run(async () =>
        {
            try
            {
                var stderr = await _ffmpegProcess.StandardError.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(stderr) && stderr.Contains("error"))
                {
                    Console.WriteLine($"VideoDeviceService: ffmpeg stderr: {stderr}");
                }
            }
            catch { }
        });

        Console.WriteLine("VideoDeviceService: Camera test started via ffmpeg command line");
    }

    private void OnVideoFrame(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
    {
        // Forward the frame to the UI
        // The sample is in the specified pixel format (usually I420 or RGB24)
        _onFrameReceived?.Invoke(sample, width, height);
    }

    public async Task StopTestAsync()
    {
        _testCts?.Cancel();
        _testCts?.Dispose();
        _testCts = null;

        // Stop ffmpeg process if running
        if (_ffmpegProcess != null)
        {
            try
            {
                if (!_ffmpegProcess.HasExited)
                {
                    _ffmpegProcess.Kill();
                    _ffmpegProcess.WaitForExit(1000);
                }
                _ffmpegProcess.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoDeviceService: Error stopping ffmpeg - {ex.Message}");
            }
            _ffmpegProcess = null;
        }

        if (_testCameraSource != null)
        {
            try
            {
                _testCameraSource.OnVideoSourceRawSample -= OnVideoFrame;
                await _testCameraSource.CloseVideo();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoDeviceService: Error closing camera - {ex.Message}");
            }
            _testCameraSource = null;
        }

        _onFrameReceived = null;
        Console.WriteLine("VideoDeviceService: Stopped camera test");
    }

    public void Dispose()
    {
        StopTestAsync().GetAwaiter().GetResult();
    }
}
