using Emgu.CV;
using Emgu.CV.CvEnum;
using SIPSorceryMedia.Abstractions;

namespace Snacka.Client.Services.WebRtc;

/// <summary>
/// Manages camera capture and H.264 encoding.
/// Extracted from WebRtcService for single responsibility.
/// </summary>
public class CameraManager : IAsyncDisposable
{
    private readonly ISettingsStore? _settingsStore;

    private VideoCapture? _videoCapture;
    private FfmpegProcessEncoder? _processEncoder;
    private CancellationTokenSource? _videoCts;
    private Task? _videoCaptureTask;
    private bool _isCameraOn;
    private int _sentCameraFrameCount;

    // Video capture settings
    private const int VideoWidth = 640;
    private const int VideoHeight = 480;
    private const int VideoFps = 15;

    /// <summary>
    /// Gets whether the camera is currently active.
    /// </summary>
    public bool IsCameraOn => _isCameraOn;

    /// <summary>
    /// Fired when a video frame has been encoded. Args: (durationRtpUnits, encodedSample)
    /// WebRtcService subscribes to this to send frames to connections.
    /// </summary>
    public event Action<uint, byte[]>? OnFrameEncoded;

    /// <summary>
    /// Fired when a local video frame is captured (for self-preview). Args: (width, height, rgbData)
    /// </summary>
    public event Action<int, int, byte[]>? OnLocalFrameCaptured;

    public CameraManager(ISettingsStore? settingsStore)
    {
        _settingsStore = settingsStore;
    }

    /// <summary>
    /// Sets the camera on or off.
    /// </summary>
    public async Task SetCameraAsync(bool enabled)
    {
        if (_isCameraOn == enabled) return;

        Console.WriteLine($"CameraManager: Camera = {enabled}");

        if (enabled)
        {
            // Start capture BEFORE setting state - if it throws, state remains false
            await StartAsync();
            _isCameraOn = true;
        }
        else
        {
            _isCameraOn = false;
            await StopAsync();
        }
    }

    /// <summary>
    /// Starts camera capture and encoding.
    /// </summary>
    public async Task StartAsync()
    {
        if (_videoCapture != null) return;

        try
        {
            // Get video device from settings
            var deviceIndex = 0;
            var devicePath = _settingsStore?.Settings.VideoDevice;
            if (!string.IsNullOrEmpty(devicePath) && int.TryParse(devicePath, out var parsed))
            {
                deviceIndex = parsed;
            }

            // Use AVFoundation on macOS, V4L2 on Linux for correct device mapping
            var backend = VideoCapture.API.Any;
            if (OperatingSystem.IsMacOS())
            {
                backend = VideoCapture.API.AVFoundation;
            }
            else if (OperatingSystem.IsLinux())
            {
                backend = VideoCapture.API.V4L2;
            }

            Console.WriteLine($"CameraManager: Starting video capture on device {deviceIndex} with backend {backend}");
            _videoCapture = new VideoCapture(deviceIndex, backend);

            if (!_videoCapture.IsOpened)
            {
                throw new InvalidOperationException($"Failed to open camera {deviceIndex}");
            }

            // Set capture properties
            _videoCapture.Set(CapProp.FrameWidth, VideoWidth);
            _videoCapture.Set(CapProp.FrameHeight, VideoHeight);
            _videoCapture.Set(CapProp.Fps, VideoFps);

            var actualWidth = (int)_videoCapture.Get(CapProp.FrameWidth);
            var actualHeight = (int)_videoCapture.Get(CapProp.FrameHeight);

            Console.WriteLine($"CameraManager: Video capture opened - {actualWidth}x{actualHeight}");

            // Create FFmpeg process encoder for H264 (hardware accelerated on most platforms)
            _processEncoder = new FfmpegProcessEncoder(actualWidth, actualHeight, VideoFps, VideoCodecsEnum.H264);
            _processEncoder.OnEncodedFrame += OnEncoderFrameEncoded;
            _processEncoder.Start();
            Console.WriteLine("CameraManager: Video encoder created for H264");

            // Start capture loop
            _videoCts = new CancellationTokenSource();
            _videoCaptureTask = Task.Run(() => CaptureLoop(actualWidth, actualHeight, _videoCts.Token));

            Console.WriteLine("CameraManager: Video capture started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CameraManager: Failed to start video capture - {ex.Message}");
            await StopAsync();
            throw;
        }
    }

    /// <summary>
    /// Stops camera capture and encoding.
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("CameraManager: Stopping video capture...");

        _videoCts?.Cancel();

        if (_videoCaptureTask != null)
        {
            try
            {
                await _videoCaptureTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                Console.WriteLine("CameraManager: Video capture task did not stop in time");
            }
            catch (OperationCanceledException) { }
            _videoCaptureTask = null;
        }

        _videoCts?.Dispose();
        _videoCts = null;

        // Dispose video encoder
        if (_processEncoder != null)
        {
            _processEncoder.OnEncodedFrame -= OnEncoderFrameEncoded;
            _processEncoder.Dispose();
            _processEncoder = null;
        }

        if (_videoCapture != null)
        {
            _videoCapture.Dispose();
            _videoCapture = null;
        }

        _isCameraOn = false;
        Console.WriteLine("CameraManager: Video capture stopped");
    }

    private void OnEncoderFrameEncoded(uint durationRtpUnits, byte[] encodedSample)
    {
        _sentCameraFrameCount++;
        if (_sentCameraFrameCount <= 5 || _sentCameraFrameCount % 100 == 0)
        {
            Console.WriteLine($"CameraManager: Encoded camera frame {_sentCameraFrameCount}, size={encodedSample.Length}");
        }

        OnFrameEncoded?.Invoke(durationRtpUnits, encodedSample);
    }

    private void CaptureLoop(int width, int height, CancellationToken token)
    {
        using var frame = new Mat();
        var frameIntervalMs = 1000 / VideoFps;
        var frameCount = 0;

        Console.WriteLine($"CameraManager: Video capture loop starting - target {width}x{height} @ {VideoFps}fps");

        while (!token.IsCancellationRequested && _videoCapture != null)
        {
            try
            {
                if (!_videoCapture.Read(frame) || frame.IsEmpty)
                {
                    Thread.Sleep(10);
                    continue;
                }

                // Get frame dimensions and raw BGR bytes (OpenCV captures in BGR format)
                var frameWidth = frame.Width;
                var frameHeight = frame.Height;
                var dataSize = frameWidth * frameHeight * 3;

                frameCount++;
                if (frameCount == 1 || frameCount % 100 == 0)
                {
                    Console.WriteLine($"CameraManager: Captured frame {frameCount} - {frameWidth}x{frameHeight}");
                }

                // Get BGR data from OpenCV frame
                var bgrData = new byte[dataSize];
                System.Runtime.InteropServices.Marshal.Copy(frame.DataPointer, bgrData, 0, dataSize);

                // Send frame to encoder (encoding happens asynchronously in FfmpegProcessEncoder)
                if (_processEncoder != null)
                {
                    try
                    {
                        // Send to FFmpeg process for encoding
                        _processEncoder.EncodeFrame(bgrData);
                    }
                    catch (Exception encodeEx)
                    {
                        if (frameCount <= 5 || frameCount % 100 == 0)
                        {
                            Console.WriteLine($"CameraManager: Encoding error on frame {frameCount}: {encodeEx.Message}");
                        }
                    }
                }

                // Fire local preview event (convert BGR to RGB)
                if (OnLocalFrameCaptured != null && frameCount % 2 == 0) // Every other frame for performance
                {
                    var rgbData = ColorSpaceConverter.BgrToRgb(bgrData, frameWidth, frameHeight);
                    OnLocalFrameCaptured.Invoke(frameWidth, frameHeight, rgbData);
                }

                Thread.Sleep(frameIntervalMs);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraManager: Video capture error - {ex.Message}");
                Console.WriteLine($"CameraManager: Stack trace: {ex.StackTrace}");
                Thread.Sleep(100);
            }
        }

        Console.WriteLine($"CameraManager: Video capture loop ended after {frameCount} frames");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
