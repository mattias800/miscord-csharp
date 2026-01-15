using System.Text.RegularExpressions;

namespace Snacka.Client.Services;

public interface IVideoDeviceService
{
    IReadOnlyList<VideoDeviceInfo> GetCameraDevices();
}

public record VideoDeviceInfo(string Path, string Name);

/// <summary>
/// Cross-platform video device enumeration service.
/// Uses native platform tools for reliable device discovery.
/// </summary>
public class VideoDeviceService : IVideoDeviceService
{
    public IReadOnlyList<VideoDeviceInfo> GetCameraDevices()
    {
        Console.WriteLine("VideoDeviceService: Enumerating camera devices...");

        if (OperatingSystem.IsMacOS())
        {
            return GetCameraDevicesViaMacOS();
        }

        if (OperatingSystem.IsLinux())
        {
            return GetCameraDevicesViaLinux();
        }

        if (OperatingSystem.IsWindows())
        {
            return GetCameraDevicesViaWindows();
        }

        Console.WriteLine("VideoDeviceService: Unsupported platform");
        return Array.Empty<VideoDeviceInfo>();
    }

    private IReadOnlyList<VideoDeviceInfo> GetCameraDevicesViaMacOS()
    {
        try
        {
            // Use Swift (built into macOS) to enumerate AVFoundation devices
            // Use the older devices(for:) API which matches OpenCV's AVFoundation backend order
            // (DiscoverySession uses a different order)
            // Write to temp file to avoid escaping issues with -e flag
            var swiftCode = @"import AVFoundation
let devices = AVCaptureDevice.devices(for: .video)
for (i, d) in devices.enumerated() {
    print(""\(i):\(d.localizedName)"")
}";

            var tempFile = Path.Combine(Path.GetTempPath(), $"snacka_camera_enum_{Guid.NewGuid():N}.swift");
            File.WriteAllText(tempFile, swiftCode);

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "swift",
                    Arguments = tempFile,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null) return Array.Empty<VideoDeviceInfo>();

                var output = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(15000); // Swift compilation can take a moment

                if (!string.IsNullOrEmpty(stderr))
                {
                    Console.WriteLine($"VideoDeviceService: Swift stderr: {stderr}");
                }

                var devices = new List<VideoDeviceInfo>();
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2 && int.TryParse(parts[0], out var index))
                    {
                        var name = parts[1].Trim();
                        devices.Add(new VideoDeviceInfo(index.ToString(), name));
                        Console.WriteLine($"  - Camera {index}: {name}");
                    }
                }

                Console.WriteLine($"VideoDeviceService: Found {devices.Count} cameras via Swift/AVFoundation (macOS)");
                return devices;
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoDeviceService: macOS enumeration failed - {ex.Message}");
            return Array.Empty<VideoDeviceInfo>();
        }
    }

    private IReadOnlyList<VideoDeviceInfo> GetCameraDevicesViaLinux()
    {
        try
        {
            // Use v4l2-ctl or enumerate /dev/video*
            var devices = new List<VideoDeviceInfo>();

            for (int i = 0; i < 10; i++)
            {
                var devicePath = $"/dev/video{i}";
                if (File.Exists(devicePath))
                {
                    var name = $"Video Device {i}";

                    // Try to get device name via v4l2-ctl
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "v4l2-ctl",
                            Arguments = $"--device={devicePath} --info",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var process = System.Diagnostics.Process.Start(psi);
                        if (process != null)
                        {
                            var output = process.StandardOutput.ReadToEnd();
                            process.WaitForExit(2000);

                            var match = Regex.Match(output, @"Card type\s*:\s*(.+)");
                            if (match.Success)
                            {
                                name = match.Groups[1].Value.Trim();
                            }
                        }
                    }
                    catch { }

                    devices.Add(new VideoDeviceInfo(i.ToString(), name));
                    Console.WriteLine($"  - Camera {i}: {name}");
                }
            }

            Console.WriteLine($"VideoDeviceService: Found {devices.Count} cameras via /dev/video* (Linux)");
            return devices;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoDeviceService: Linux enumeration failed - {ex.Message}");
            return Array.Empty<VideoDeviceInfo>();
        }
    }

    private IReadOnlyList<VideoDeviceInfo> GetCameraDevicesViaWindows()
    {
        try
        {
            // Use PowerShell to enumerate cameras via WMI
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-Command \"Get-CimInstance Win32_PnPEntity | Where-Object { $_.PNPClass -eq 'Camera' -or $_.PNPClass -eq 'Image' } | Select-Object -ExpandProperty Name\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return Array.Empty<VideoDeviceInfo>();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var devices = new List<VideoDeviceInfo>();
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                var name = lines[i].Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    devices.Add(new VideoDeviceInfo(i.ToString(), name));
                    Console.WriteLine($"  - Camera {i}: {name}");
                }
            }

            Console.WriteLine($"VideoDeviceService: Found {devices.Count} cameras via PowerShell/WMI (Windows)");
            return devices;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoDeviceService: Windows enumeration failed - {ex.Message}");
            return Array.Empty<VideoDeviceInfo>();
        }
    }
}
