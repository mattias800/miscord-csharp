using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;
using Snacka.Client.Services;

namespace Snacka.Client.ViewModels;

public class VideoSettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settingsStore;
    private readonly IVideoDeviceService _videoDeviceService;

    private string? _selectedVideoDevice;
    private bool _isTestingCamera;
    private bool _isLoadingDevices;
    private int _frameCount;
    private int _frameWidth;
    private int _frameHeight;
    private string _cameraStatus = "Not testing";
    private WriteableBitmap? _previewBitmap;

    public VideoSettingsViewModel(ISettingsStore settingsStore, IVideoDeviceService videoDeviceService)
    {
        _settingsStore = settingsStore;
        _videoDeviceService = videoDeviceService;

        VideoDevices = new ObservableCollection<VideoDeviceItem>();

        TestCameraCommand = ReactiveCommand.CreateFromTask(ToggleCameraTest);
        RefreshDevicesCommand = ReactiveCommand.CreateFromTask(RefreshDevicesAsync);

        // Load saved selection
        _selectedVideoDevice = _settingsStore.Settings.VideoDevice;

        // Add default option immediately so UI has something to show
        VideoDevices.Add(new VideoDeviceItem(null, "Default"));
    }

    /// <summary>
    /// Initialize device lists asynchronously. Call this after construction.
    /// </summary>
    public Task InitializeAsync() => RefreshDevicesAsync();

    public ObservableCollection<VideoDeviceItem> VideoDevices { get; }

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

    public int FrameCount
    {
        get => _frameCount;
        set => this.RaiseAndSetIfChanged(ref _frameCount, value);
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

            // Add default option
            VideoDevices.Add(new VideoDeviceItem(null, "Default"));

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
            await _videoDeviceService.StopTestAsync();
            IsTestingCamera = false;
            FrameCount = 0;
            _frameWidth = 0;
            _frameHeight = 0;
            CameraStatus = "Not testing";
            this.RaisePropertyChanged(nameof(Resolution));
        }
        else
        {
            try
            {
                FrameCount = 0;
                CameraStatus = "Starting...";

                await _videoDeviceService.StartCameraTestAsync(
                    _selectedVideoDevice,
                    (frameData, width, height) => Dispatcher.UIThread.Post(() =>
                    {
                        OnFrameReceived(frameData, width, height);
                    })
                );
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
        await _videoDeviceService.StopTestAsync();
        FrameCount = 0;
        _frameWidth = 0;
        _frameHeight = 0;
        CameraStatus = "Restarting...";
        this.RaisePropertyChanged(nameof(Resolution));

        try
        {
            await _videoDeviceService.StartCameraTestAsync(
                _selectedVideoDevice,
                (frameData, width, height) => Dispatcher.UIThread.Post(() =>
                {
                    OnFrameReceived(frameData, width, height);
                })
            );
            CameraStatus = "Receiving frames";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoSettings: Failed to restart camera test - {ex.Message}");
            IsTestingCamera = false;
            CameraStatus = $"Error: {ex.Message}";
        }
    }

    private void OnFrameReceived(byte[] frameData, int width, int height)
    {
        FrameCount++;

        if (_frameWidth != width || _frameHeight != height)
        {
            _frameWidth = width;
            _frameHeight = height;
            this.RaisePropertyChanged(nameof(Resolution));
        }

        // Create new bitmap each frame - Avalonia needs a new object to detect changes
        if (frameData.Length == width * height * 3)
        {
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
                    bgraData[i * 4 + 0] = frameData[rgbIndex + 2]; // B
                    bgraData[i * 4 + 1] = frameData[rgbIndex + 1]; // G
                    bgraData[i * 4 + 2] = frameData[rgbIndex + 0]; // R
                    bgraData[i * 4 + 3] = 255;                     // A
                    rgbIndex += 3;
                }

                System.Runtime.InteropServices.Marshal.Copy(bgraData, 0, destPtr, bgraData.Length);
            }

            // Assign new bitmap - this triggers UI update
            PreviewBitmap = bitmap;
        }
    }

    private async Task StopCameraTestAsync()
    {
        await _videoDeviceService.StopTestAsync();
        IsTestingCamera = false;
        FrameCount = 0;
        _frameWidth = 0;
        _frameHeight = 0;
        PreviewBitmap = null;
        CameraStatus = "Not testing";
        this.RaisePropertyChanged(nameof(Resolution));
    }
}

public record VideoDeviceItem(string? Value, string DisplayName);
