using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;
using Snacka.Client.Services;

namespace Snacka.Client.ViewModels;

/// <summary>
/// Option item for resolution dropdown.
/// </summary>
public record ResolutionOption(string Value, string DisplayName);

/// <summary>
/// Option item for framerate dropdown.
/// </summary>
public record FramerateOption(int Value, string DisplayName);

/// <summary>
/// Option item for bitrate dropdown.
/// </summary>
public record BitrateOption(int Value, string DisplayName);

public class VideoSettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settingsStore;
    private readonly IVideoDeviceService _videoDeviceService;
    private readonly CameraTestService _cameraTestService;

    private string? _selectedVideoDevice;
    private bool _isTestingCamera;
    private bool _isLoadingDevices;
    private int _rawFrameCount;
    private int _encodedFrameCount;
    private int _frameWidth;
    private int _frameHeight;
    private string _cameraStatus = "Not testing";
    private WriteableBitmap? _previewBitmap;
    private WriteableBitmap? _encodedPreviewBitmap;

    // Quality settings
    private string _selectedResolution;
    private int _selectedFramerate;
    private int _selectedBitrate;

    public VideoSettingsViewModel(ISettingsStore settingsStore, IVideoDeviceService videoDeviceService)
    {
        _settingsStore = settingsStore;
        _videoDeviceService = videoDeviceService;
        _cameraTestService = new CameraTestService();

        VideoDevices = new ObservableCollection<VideoDeviceItem>();

        // Initialize quality options
        ResolutionOptions = new ObservableCollection<ResolutionOption>
        {
            new("640x480", "480p (640x480)"),
            new("1280x720", "720p (1280x720)"),
            new("1920x1080", "1080p (1920x1080)")
        };

        FramerateOptions = new ObservableCollection<FramerateOption>
        {
            new(15, "15 fps"),
            new(30, "30 fps")
        };

        BitrateOptions = new ObservableCollection<BitrateOption>
        {
            new(1, "Low (1 Mbps)"),
            new(2, "Medium (2 Mbps)"),
            new(4, "High (4 Mbps)")
        };

        TestCameraCommand = ReactiveCommand.CreateFromTask(ToggleCameraTest);
        RefreshDevicesCommand = ReactiveCommand.CreateFromTask(RefreshDevicesAsync);

        // Load saved selections
        _selectedVideoDevice = _settingsStore.Settings.VideoDevice;
        _selectedResolution = _settingsStore.Settings.CameraResolution;
        _selectedFramerate = _settingsStore.Settings.CameraFramerate;
        _selectedBitrate = _settingsStore.Settings.CameraBitrateMbps;

        // Wire up camera test service events
        _cameraTestService.OnRawFrameReceived += (width, height, rgbData) =>
            Dispatcher.UIThread.Post(() => OnRawFrameReceived(rgbData, width, height));
        _cameraTestService.OnEncodedFrameReceived += (width, height, rgbData) =>
            Dispatcher.UIThread.Post(() => OnEncodedFrameReceived(rgbData, width, height));
        _cameraTestService.OnError += error =>
            Dispatcher.UIThread.Post(() => CameraStatus = $"Error: {error}");

        // Add "None" option immediately so UI has something to show
        VideoDevices.Add(new VideoDeviceItem(null, "None"));
    }

    /// <summary>
    /// Initialize device lists asynchronously. Call this after construction.
    /// </summary>
    public Task InitializeAsync() => RefreshDevicesAsync();

    public ObservableCollection<VideoDeviceItem> VideoDevices { get; }

    // Quality options
    public ObservableCollection<ResolutionOption> ResolutionOptions { get; }
    public ObservableCollection<FramerateOption> FramerateOptions { get; }
    public ObservableCollection<BitrateOption> BitrateOptions { get; }

    public string SelectedResolution
    {
        get => _selectedResolution;
        set
        {
            if (_selectedResolution == value) return;
            this.RaiseAndSetIfChanged(ref _selectedResolution, value);
            _settingsStore.Settings.CameraResolution = value;
            _settingsStore.Save();

            // Restart test with new resolution if testing
            if (_isTestingCamera)
            {
                _ = RestartCameraTest();
            }
        }
    }

    public int SelectedFramerate
    {
        get => _selectedFramerate;
        set
        {
            if (_selectedFramerate == value) return;
            this.RaiseAndSetIfChanged(ref _selectedFramerate, value);
            _settingsStore.Settings.CameraFramerate = value;
            _settingsStore.Save();

            // Restart test with new framerate if testing
            if (_isTestingCamera)
            {
                _ = RestartCameraTest();
            }
        }
    }

    public int SelectedBitrate
    {
        get => _selectedBitrate;
        set
        {
            if (_selectedBitrate == value) return;
            this.RaiseAndSetIfChanged(ref _selectedBitrate, value);
            _settingsStore.Settings.CameraBitrateMbps = value;
            _settingsStore.Save();

            // Restart test with new bitrate if testing
            if (_isTestingCamera)
            {
                _ = RestartCameraTest();
            }
        }
    }

    public string? SelectedVideoDevice
    {
        get => _selectedVideoDevice;
        set
        {
            if (_selectedVideoDevice == value) return;

            this.RaiseAndSetIfChanged(ref _selectedVideoDevice, value);
            _settingsStore.Settings.VideoDevice = value;
            _settingsStore.Save();

            // Restart test with new device if testing
            if (_isTestingCamera)
            {
                _ = RestartCameraTest();
            }
        }
    }

    public bool IsTestingCamera
    {
        get => _isTestingCamera;
        set => this.RaiseAndSetIfChanged(ref _isTestingCamera, value);
    }

    public int RawFrameCount
    {
        get => _rawFrameCount;
        set => this.RaiseAndSetIfChanged(ref _rawFrameCount, value);
    }

    public int EncodedFrameCount
    {
        get => _encodedFrameCount;
        set => this.RaiseAndSetIfChanged(ref _encodedFrameCount, value);
    }

    public string CameraStatus
    {
        get => _cameraStatus;
        set => this.RaiseAndSetIfChanged(ref _cameraStatus, value);
    }

    public string Resolution => _frameWidth > 0 ? $"{_frameWidth}x{_frameHeight}" : "—";

    public WriteableBitmap? PreviewBitmap
    {
        get => _previewBitmap;
        set => this.RaiseAndSetIfChanged(ref _previewBitmap, value);
    }

    public WriteableBitmap? EncodedPreviewBitmap
    {
        get => _encodedPreviewBitmap;
        set => this.RaiseAndSetIfChanged(ref _encodedPreviewBitmap, value);
    }

    public bool IsLoadingDevices
    {
        get => _isLoadingDevices;
        private set => this.RaiseAndSetIfChanged(ref _isLoadingDevices, value);
    }

    public ICommand TestCameraCommand { get; }
    public ICommand RefreshDevicesCommand { get; }

    private async Task RefreshDevicesAsync()
    {
        IsLoadingDevices = true;

        try
        {
            // Run device enumeration on background thread to avoid blocking UI
            var devices = await Task.Run(() => _videoDeviceService.GetCameraDevices());

            // Update collection on UI thread
            VideoDevices.Clear();

            // Add "None" option
            VideoDevices.Add(new VideoDeviceItem(null, "None"));

            // Add available devices
            foreach (var device in devices)
            {
                VideoDevices.Add(new VideoDeviceItem(device.Path, device.Name));
            }
        }
        finally
        {
            IsLoadingDevices = false;
        }
    }

    private async Task ToggleCameraTest()
    {
        if (_isTestingCamera)
        {
            await _cameraTestService.StopAsync();
            IsTestingCamera = false;
            RawFrameCount = 0;
            EncodedFrameCount = 0;
            _frameWidth = 0;
            _frameHeight = 0;
            CameraStatus = "Not testing";
            PreviewBitmap = null;
            EncodedPreviewBitmap = null;
            this.RaisePropertyChanged(nameof(Resolution));
        }
        else
        {
            try
            {
                RawFrameCount = 0;
                EncodedFrameCount = 0;
                CameraStatus = "Starting...";

                // Get camera ID - if null, use "0" as default
                var cameraId = _selectedVideoDevice ?? "0";

                await _cameraTestService.StartAsync(cameraId, _selectedResolution, _selectedFramerate, _selectedBitrate);
                IsTestingCamera = true;
                CameraStatus = "Receiving frames";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoSettings: Failed to start camera test - {ex.Message}");
                IsTestingCamera = false;
                CameraStatus = $"Error: {ex.Message}";
            }
        }
    }

    private async Task RestartCameraTest()
    {
        await _cameraTestService.StopAsync();
        RawFrameCount = 0;
        EncodedFrameCount = 0;
        _frameWidth = 0;
        _frameHeight = 0;
        PreviewBitmap = null;
        EncodedPreviewBitmap = null;
        CameraStatus = "Restarting...";
        this.RaisePropertyChanged(nameof(Resolution));

        try
        {
            var cameraId = _selectedVideoDevice ?? "0";
            await _cameraTestService.StartAsync(cameraId, _selectedResolution, _selectedFramerate, _selectedBitrate);
            CameraStatus = "Receiving frames";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoSettings: Failed to restart camera test - {ex.Message}");
            IsTestingCamera = false;
            CameraStatus = $"Error: {ex.Message}";
        }
    }

    private void OnRawFrameReceived(byte[] frameData, int width, int height)
    {
        RawFrameCount++;

        if (_frameWidth != width || _frameHeight != height)
        {
            _frameWidth = width;
            _frameHeight = height;
            this.RaisePropertyChanged(nameof(Resolution));
        }

        // Create bitmap from RGB24 data
        PreviewBitmap = CreateBitmapFromRgb(frameData, width, height);
    }

    private void OnEncodedFrameReceived(byte[] frameData, int width, int height)
    {
        EncodedFrameCount++;

        // Create bitmap from RGB24 data
        EncodedPreviewBitmap = CreateBitmapFromRgb(frameData, width, height);
    }

    private static WriteableBitmap CreateBitmapFromRgb(byte[] rgbData, int width, int height)
    {
        if (rgbData.Length != width * height * 3)
        {
            Console.WriteLine($"VideoSettings: Invalid RGB data length {rgbData.Length}, expected {width * height * 3}");
            return null!;
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using (var lockedBitmap = bitmap.Lock())
        {
            var destPtr = lockedBitmap.Address;
            var rgbIndex = 0;
            var bgraData = new byte[width * height * 4];

            for (int i = 0; i < width * height; i++)
            {
                bgraData[i * 4 + 0] = rgbData[rgbIndex + 2]; // B
                bgraData[i * 4 + 1] = rgbData[rgbIndex + 1]; // G
                bgraData[i * 4 + 2] = rgbData[rgbIndex + 0]; // R
                bgraData[i * 4 + 3] = 255;                   // A
                rgbIndex += 3;
            }

            System.Runtime.InteropServices.Marshal.Copy(bgraData, 0, destPtr, bgraData.Length);
        }

        return bitmap;
    }

    private async Task StopCameraTestAsync()
    {
        await _cameraTestService.StopAsync();
        IsTestingCamera = false;
        RawFrameCount = 0;
        EncodedFrameCount = 0;
        _frameWidth = 0;
        _frameHeight = 0;
        PreviewBitmap = null;
        EncodedPreviewBitmap = null;
        CameraStatus = "Not testing";
        this.RaisePropertyChanged(nameof(Resolution));
    }
}

public record VideoDeviceItem(string? Value, string DisplayName);
