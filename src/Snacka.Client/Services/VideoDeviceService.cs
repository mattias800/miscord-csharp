using System.Text.Json;
using System.Text.Json.Serialization;
using Snacka.Client.Services.WebRtc;

namespace Snacka.Client.Services;

public interface IVideoDeviceService
{
    IReadOnlyList<VideoDeviceInfo> GetCameraDevices();
}

public record VideoDeviceInfo(string Path, string Name);

#region Native Capture JSON Models

internal record NativeCaptureSourceList(
    [property: JsonPropertyName("displays")] List<NativeCaptureDisplay>? Displays,
    [property: JsonPropertyName("windows")] List<NativeCaptureWindow>? Windows,
    [property: JsonPropertyName("applications")] List<NativeCaptureApplication>? Applications,
    [property: JsonPropertyName("cameras")] List<NativeCaptureCamera>? Cameras
);

internal record NativeCaptureDisplay(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height
);

internal record NativeCaptureWindow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("appName")] string? AppName,
    [property: JsonPropertyName("bundleId")] string? BundleId
);

internal record NativeCaptureApplication(
    [property: JsonPropertyName("bundleId")] string? BundleId,
    [property: JsonPropertyName("name")] string Name
);

internal record NativeCaptureCamera(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("position")] string? Position  // macOS only: "front", "back", "unspecified"
);

#endregion

/// <summary>
/// Cross-platform video device enumeration service.
/// Uses native capture tools for reliable device discovery.
/// </summary>
public class VideoDeviceService : IVideoDeviceService
{
    private readonly NativeCaptureLocator _captureLocator = new();

    public IReadOnlyList<VideoDeviceInfo> GetCameraDevices()
    {
        Console.WriteLine("VideoDeviceService: Enumerating camera devices...");

        // Try native capture tool first
        var devices = GetCameraDevicesViaNativeTool();
        if (devices.Count > 0)
        {
            return devices;
        }

        Console.WriteLine("VideoDeviceService: No cameras found or native tool unavailable");
        return Array.Empty<VideoDeviceInfo>();
    }

    private IReadOnlyList<VideoDeviceInfo> GetCameraDevicesViaNativeTool()
    {
        string? capturePath = null;

        if (OperatingSystem.IsMacOS() && _captureLocator.ShouldUseSnackaCaptureVideoToolbox())
        {
            capturePath = _captureLocator.GetSnackaCaptureVideoToolboxPath();
        }
        else if (OperatingSystem.IsWindows() && _captureLocator.ShouldUseSnackaCaptureWindows())
        {
            capturePath = _captureLocator.GetSnackaCaptureWindowsPath();
        }
        else if (OperatingSystem.IsLinux() && _captureLocator.ShouldUseSnackaCaptureLinux())
        {
            capturePath = _captureLocator.GetSnackaCaptureLinuxPath();
        }

        if (capturePath == null)
        {
            Console.WriteLine("VideoDeviceService: No native capture tool available");
            return Array.Empty<VideoDeviceInfo>();
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = capturePath,
                Arguments = "list --json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return Array.Empty<VideoDeviceInfo>();

            var output = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);

            if (!string.IsNullOrEmpty(stderr))
            {
                Console.WriteLine($"VideoDeviceService: Native tool stderr: {stderr}");
            }

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"VideoDeviceService: Native tool exited with code {process.ExitCode}");
                return Array.Empty<VideoDeviceInfo>();
            }

            var sourceList = JsonSerializer.Deserialize<NativeCaptureSourceList>(output);
            if (sourceList?.Cameras == null || sourceList.Cameras.Count == 0)
            {
                Console.WriteLine("VideoDeviceService: No cameras in native tool output");
                return Array.Empty<VideoDeviceInfo>();
            }

            var devices = new List<VideoDeviceInfo>();
            foreach (var camera in sourceList.Cameras)
            {
                // Use unique ID as path for selection (stable across reboots), name for display
                // The native capture tool accepts both unique ID and index, preferring unique ID
                devices.Add(new VideoDeviceInfo(camera.Id, camera.Name));
                Console.WriteLine($"  - Camera [{camera.Index}] {camera.Id}: {camera.Name}");
            }

            var platform = OperatingSystem.IsMacOS() ? "macOS" :
                          OperatingSystem.IsWindows() ? "Windows" : "Linux";
            Console.WriteLine($"VideoDeviceService: Found {devices.Count} cameras via native tool ({platform})");
            return devices;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoDeviceService: Native tool enumeration failed - {ex.Message}");
            return Array.Empty<VideoDeviceInfo>();
        }
    }
}
