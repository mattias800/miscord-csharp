using System.Reflection;
using System.Runtime.InteropServices;
using SIPSorceryMedia.Encoders;

namespace Snacka.Client.Services;

/// <summary>
/// Static class for initializing native libraries required for video/audio processing.
/// Handles platform-specific library loading for FFmpeg and VPX codecs.
/// </summary>
public static class NativeLibraryInitializer
{
    private static bool _ffmpegInitialized;
    private static readonly object _ffmpegInitLock = new();
    private static bool _vpxInitialized;
    private static readonly object _vpxInitLock = new();
    private static bool _sdl2AudioInitialized;
    private static readonly object _sdl2AudioInitLock = new();

    // SDL2 P/Invoke for audio initialization
    [DllImport("SDL2")]
    private static extern int SDL_Init(uint flags);
    private const uint SDL_INIT_AUDIO = 0x00000010;

    // VPX library paths for macOS
    private static readonly string[] VpxPaths =
    {
        "/opt/homebrew/lib/libvpx.dylib",      // Apple Silicon Homebrew
        "/opt/homebrew/opt/libvpx/lib/libvpx.dylib",
        "/usr/local/lib/libvpx.dylib",         // Intel Homebrew
        "/usr/lib/libvpx.dylib",               // System
        "libvpx.dylib",                        // Current directory / PATH
        "libvpx"                               // Let system find it
    };

    private static IntPtr ResolveVpx(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Handle vpxmd (Windows name) -> libvpx (macOS/Linux name)
        if (libraryName == "vpxmd" || libraryName == "libvpx" || libraryName == "vpx")
        {
            foreach (var path in VpxPaths)
            {
                if (NativeLibrary.TryLoad(path, out var handle))
                {
                    Console.WriteLine($"NativeLibrary: Loaded VPX from {path}");
                    return handle;
                }
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Ensures VPX codec libraries are initialized for video encoding.
    /// Thread-safe and only runs once.
    /// </summary>
    public static void EnsureVpxInitialized()
    {
        if (_vpxInitialized) return;

        lock (_vpxInitLock)
        {
            if (_vpxInitialized) return;

            try
            {
                if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                {
                    // Register DllImportResolver for the VPX encoder assembly
                    var encoderAssembly = typeof(VideoEncoderEndPoint).Assembly;
                    NativeLibrary.SetDllImportResolver(encoderAssembly, ResolveVpx);
                    Console.WriteLine($"NativeLibrary: Registered VPX DllImportResolver for {encoderAssembly.GetName().Name}");
                }

                _vpxInitialized = true;
                Console.WriteLine("NativeLibrary: VPX initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NativeLibrary: Failed to initialize VPX - {ex.Message}");
                _vpxInitialized = true; // Mark as attempted
            }
        }
    }

    /// <summary>
    /// Ensures FFmpeg libraries are initialized for video encoding/decoding.
    /// Thread-safe and only runs once.
    /// </summary>
    public static void EnsureFfmpegInitialized()
    {
        if (_ffmpegInitialized) return;

        lock (_ffmpegInitLock)
        {
            if (_ffmpegInitialized) return;

            try
            {
                // Set FFmpeg library path for macOS
                // Use FFmpeg 6.x which is compatible with FFmpeg.AutoGen 8.0.0
                if (OperatingSystem.IsMacOS())
                {
                    // Try versioned FFmpeg 6 first (compatible with FFmpeg.AutoGen 8.0.0)
                    // Then fall back to default paths
                    var paths = new[]
                    {
                        "/opt/homebrew/opt/ffmpeg@6/lib",  // Apple Silicon Homebrew FFmpeg 6
                        "/usr/local/opt/ffmpeg@6/lib",     // Intel Homebrew FFmpeg 6
                        "/opt/homebrew/lib",               // Apple Silicon Homebrew (default)
                        "/usr/local/lib",                  // Intel Homebrew (default)
                        "/usr/lib"                         // System
                    };

                    foreach (var path in paths)
                    {
                        if (Directory.Exists(path) && File.Exists(Path.Combine(path, "libavcodec.dylib")))
                        {
                            FFmpeg.AutoGen.ffmpeg.RootPath = path;
                            Console.WriteLine($"NativeLibrary: FFmpeg path set to {path}");
                            break;
                        }
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    var paths = new[]
                    {
                        "/usr/lib/x86_64-linux-gnu",
                        "/usr/lib",
                        "/usr/local/lib"
                    };

                    foreach (var path in paths)
                    {
                        if (Directory.Exists(path) && File.Exists(Path.Combine(path, "libavcodec.so")))
                        {
                            FFmpeg.AutoGen.ffmpeg.RootPath = path;
                            Console.WriteLine($"NativeLibrary: FFmpeg path set to {path}");
                            break;
                        }
                    }
                }

                _ffmpegInitialized = true;
                Console.WriteLine("NativeLibrary: FFmpeg initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NativeLibrary: Failed to initialize FFmpeg - {ex.Message}");
                _ffmpegInitialized = true; // Mark as attempted
            }
        }
    }

    /// <summary>
    /// Ensures SDL2 audio subsystem is initialized.
    /// Required before using SDL2AudioSource for microphone capture.
    /// Thread-safe and only runs once.
    /// </summary>
    public static void EnsureSdl2AudioInitialized()
    {
        if (_sdl2AudioInitialized) return;

        lock (_sdl2AudioInitLock)
        {
            if (_sdl2AudioInitialized) return;

            try
            {
                SDL_Init(SDL_INIT_AUDIO);
                _sdl2AudioInitialized = true;
                Console.WriteLine("NativeLibrary: SDL2 audio initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NativeLibrary: Failed to initialize SDL2 audio - {ex.Message}");
                _sdl2AudioInitialized = true; // Mark as attempted
            }
        }
    }

    /// <summary>
    /// Initializes all native libraries required for WebRTC.
    /// Call this once during application startup.
    /// </summary>
    public static void InitializeAll()
    {
        EnsureFfmpegInitialized();
        EnsureVpxInitialized();
        EnsureSdl2AudioInitialized();
    }
}
