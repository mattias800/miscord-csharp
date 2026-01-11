using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Snacka.Client;

/// <summary>
/// Standalone audio test to debug LibVLC initialization.
/// Run with: ./run-client.sh --audio-test
/// Note: VLC_PLUGIN_PATH must be set before process starts for audio to work.
/// </summary>
public static class AudioTest
{
    public static void Run()
    {
        Console.WriteLine("=== LibVLC Audio Test ===\n");

        // Find an MP3 file in Downloads
        var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Console.WriteLine($"Looking for MP3 files in: {downloadsPath}");

        var mp3Files = Directory.GetFiles(downloadsPath, "*.mp3", SearchOption.TopDirectoryOnly);
        if (mp3Files.Length == 0)
        {
            Console.WriteLine("No MP3 files found in Downloads folder.");
            Console.WriteLine("Please place an MP3 file in ~/Downloads and try again.");
            return;
        }

        var testFile = mp3Files[0];
        Console.WriteLine($"Found: {Path.GetFileName(testFile)}\n");

        // Check VLC environment
        var vlcPluginPath = Environment.GetEnvironmentVariable("VLC_PLUGIN_PATH");
        Console.WriteLine($"VLC_PLUGIN_PATH: {vlcPluginPath ?? "(not set)"}");
        if (string.IsNullOrEmpty(vlcPluginPath))
        {
            Console.WriteLine("\nWARNING: VLC_PLUGIN_PATH is not set!");
            Console.WriteLine("Run using: ./run-client.sh --audio-test");
            Console.WriteLine("Or set VLC_PLUGIN_PATH=/Applications/VLC.app/Contents/MacOS/plugins before running.");
            return;
        }

        TryPlayAudio(testFile);
    }

    private static void TryPlayAudio(string filePath)
    {
        LibVLC? libVLC = null;
        MediaPlayer? player = null;

        try
        {
            // Initialize with VLC.app path on macOS
            var vlcPath = "/Applications/VLC.app/Contents/MacOS/lib";
            if (Directory.Exists(vlcPath))
            {
                Console.WriteLine($"Initializing LibVLC with: {vlcPath}");
                Core.Initialize(vlcPath);
            }
            else
            {
                Console.WriteLine("Initializing LibVLC with default path");
                Core.Initialize();
            }

            Console.WriteLine("Creating LibVLC instance...");
            libVLC = new LibVLC("--no-video");
            Console.WriteLine("LibVLC instance created!");

            Console.WriteLine($"Creating Media from file: {filePath}");
            using var media = new Media(libVLC, filePath, FromType.FromPath);
            Console.WriteLine("Media created!");

            Console.WriteLine("Creating MediaPlayer...");
            player = new MediaPlayer(media);
            Console.WriteLine("MediaPlayer created!");

            Console.WriteLine("Starting playback...");
            player.Play();
            Console.WriteLine("Play() called!");

            Console.WriteLine("\nPlaying audio for 5 seconds... Press Ctrl+C to stop.");
            Thread.Sleep(5000);

            Console.WriteLine("Stopping...");
            player.Stop();
            Console.WriteLine("Done!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
        }
        finally
        {
            player?.Dispose();
            libVLC?.Dispose();
        }
    }
}
