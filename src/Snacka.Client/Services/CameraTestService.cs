using System.Diagnostics;
using Snacka.Client.Services.HardwareVideo;
using Snacka.Client.Services.WebRtc;

namespace Snacka.Client.Services;

/// <summary>
/// Service for testing camera with hardware-accelerated preview.
/// Used in video settings to show users what their camera looks like.
/// Uses the same hardware decoder pipeline as actual streaming for consistency.
/// </summary>
public class CameraTestService : IDisposable
{
    private readonly NativeCaptureLocator _captureLocator;
    private Process? _captureProcess;
    private IHardwareVideoDecoder? _hardwareDecoder;
    private Task? _stderrParserTask;
    private Task? _h264ReaderTask;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    private int _frameCount;
    private int _previewWidth;
    private int _previewHeight;
    private byte[]? _sps;
    private byte[]? _pps;

    /// <summary>
    /// Fired when the hardware decoder is ready for embedding.
    /// The UI should embed the decoder's native view.
    /// </summary>
    public event Action<IHardwareVideoDecoder>? OnHardwareDecoderReady;

    /// <summary>
    /// Fired when a decoded H.264 frame is received (NV12 format for GPU rendering).
    /// This is a fallback when hardware decoding is not available.
    /// Parameters: width, height, nv12Data
    /// </summary>
    public event Action<int, int, byte[]>? OnPreviewFrameReceived;

    /// <summary>
    /// Fired when an error occurs during capture/decode.
    /// </summary>
    public event Action<string>? OnError;

    public bool IsRunning => _isRunning;
    public int FrameCount => _frameCount;

    /// <summary>
    /// Gets the hardware decoder if available (for embedding native view).
    /// </summary>
    public IHardwareVideoDecoder? HardwareDecoder => _hardwareDecoder;

    public CameraTestService()
    {
        _captureLocator = new NativeCaptureLocator();
    }

    /// <summary>
    /// Starts camera test with hardware-accelerated preview.
    /// </summary>
    /// <param name="cameraId">Camera device ID or index</param>
    /// <param name="height">Video height (480, 720, 1080). Width calculated assuming 16:9 aspect ratio.</param>
    /// <param name="fps">Frame rate</param>
    /// <param name="bitrateMbps">Encoding bitrate in Mbps</param>
    public async Task StartAsync(string cameraId, int height, int fps, int bitrateMbps)
    {
        if (_isRunning)
        {
            await StopAsync();
        }

        _frameCount = 0;
        _sps = null;
        _pps = null;
        _cts = new CancellationTokenSource();

        // Calculate width assuming 16:9 aspect ratio (most common for webcams)
        var width = CalculateWidthFor16x9(height);
        _previewWidth = width;
        _previewHeight = height;

        // Get native capture tool path
        var capturePath = _captureLocator.GetNativeCameraCapturePath();
        if (capturePath == null)
        {
            OnError?.Invoke("Native capture tool not found. Ensure SnackaCaptureVideoToolbox (macOS), SnackaCaptureWindows, or SnackaCaptureLinux is built.");
            return;
        }

        // Build arguments
        var args = _captureLocator.GetNativeCameraCaptureArgs(cameraId, width, height, fps, bitrateMbps);

        Console.WriteLine($"CameraTestService: Starting capture at {width}x{height}@{fps}fps");
        Console.WriteLine($"CameraTestService: Command: {capturePath} {args}");

        try
        {
            // Start native capture process
            var startInfo = new ProcessStartInfo
            {
                FileName = capturePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _captureProcess = new Process { StartInfo = startInfo };
            _captureProcess.Start();

            _isRunning = true;

            // Start parsing stderr for log messages
            var token = _cts.Token;
            _stderrParserTask = Task.Run(() => ParseStderrLoop(token), token);

            // Start reading H.264 NAL units and feeding to hardware decoder
            _h264ReaderTask = Task.Run(() => ReadH264AndDecode(token), token);

            Console.WriteLine($"CameraTestService: Capture started, pid={_captureProcess.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CameraTestService: Failed to start - {ex.Message}");
            OnError?.Invoke($"Failed to start capture: {ex.Message}");
            await StopAsync();
        }
    }

    private void ParseStderrLoop(CancellationToken token)
    {
        if (_captureProcess == null) return;

        try
        {
            var parser = new StderrPacketParser(_captureProcess.StandardError.BaseStream);

            parser.OnLogMessage += message =>
            {
                Console.WriteLine($"CameraTestService (native log): {message}");
            };

            parser.ParseLoop(token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Console.WriteLine($"CameraTestService: Stderr parser error - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reads H.264 NAL units from the capture process and decodes using hardware decoder.
    /// This mirrors the approach used in CameraManager for consistent behavior.
    /// </summary>
    private void ReadH264AndDecode(CancellationToken token)
    {
        if (_captureProcess == null) return;

        var lengthBuffer = new byte[4];

        try
        {
            var stream = _captureProcess.StandardOutput.BaseStream;

            while (!token.IsCancellationRequested && _captureProcess != null && !_captureProcess.HasExited)
            {
                // Read 4-byte NAL length prefix (big-endian AVCC format)
                var bytesRead = 0;
                while (bytesRead < 4 && !token.IsCancellationRequested)
                {
                    var read = stream.Read(lengthBuffer, bytesRead, 4 - bytesRead);
                    if (read == 0) break;
                    bytesRead += read;
                }

                if (bytesRead < 4) break;

                // Parse NAL length (big-endian)
                var nalLength = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) |
                               (lengthBuffer[2] << 8) | lengthBuffer[3];

                if (nalLength <= 0 || nalLength > 10_000_000)
                {
                    Console.WriteLine($"CameraTestService: Invalid NAL length {nalLength}, skipping");
                    continue;
                }

                // Read NAL data
                var nalData = new byte[nalLength];
                bytesRead = 0;
                while (bytesRead < nalLength && !token.IsCancellationRequested)
                {
                    var read = stream.Read(nalData, bytesRead, nalLength - bytesRead);
                    if (read == 0) break;
                    bytesRead += read;
                }

                if (bytesRead < nalLength) break;

                // Parse NAL type
                var nalType = nalData[0] & 0x1F;

                // Store SPS/PPS for hardware decoder initialization
                if (nalType == 7) // SPS
                {
                    _sps = nalData;
                    Console.WriteLine($"CameraTestService: Stored SPS ({nalData.Length} bytes)");
                    TryInitializeHardwareDecoder();
                }
                else if (nalType == 8) // PPS
                {
                    _pps = nalData;
                    Console.WriteLine($"CameraTestService: Stored PPS ({nalData.Length} bytes)");
                    TryInitializeHardwareDecoder();
                }

                // Feed VCL NAL units to hardware decoder
                if (_hardwareDecoder != null && (nalType == 1 || nalType == 5))
                {
                    var isKeyframe = nalType == 5;
                    _hardwareDecoder.DecodeAndRender(nalData, isKeyframe);
                    _frameCount++;

                    if (_frameCount <= 5 || _frameCount % 100 == 0)
                    {
                        Console.WriteLine($"CameraTestService: Decoded frame {_frameCount} (NAL type {nalType})");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Console.WriteLine($"CameraTestService: H.264 read error - {ex.Message}");
            }
        }

        Console.WriteLine($"CameraTestService: H.264 reader ended after {_frameCount} frames");
    }

    /// <summary>
    /// Tries to initialize the hardware decoder when both SPS and PPS are available.
    /// </summary>
    private void TryInitializeHardwareDecoder()
    {
        if (_hardwareDecoder != null)
            return;

        if (_sps == null || _pps == null)
            return;

        if (!HardwareVideoDecoderFactory.IsAvailable())
        {
            Console.WriteLine("CameraTestService: Hardware decoding not available");
            OnError?.Invoke("Hardware video decoding not available on this system");
            return;
        }

        Console.WriteLine("CameraTestService: Creating hardware decoder...");
        var decoder = HardwareVideoDecoderFactory.Create();
        if (decoder == null)
        {
            Console.WriteLine("CameraTestService: Failed to create hardware decoder");
            OnError?.Invoke("Failed to create hardware decoder");
            return;
        }

        if (decoder.Initialize(_previewWidth, _previewHeight, _sps, _pps))
        {
            _hardwareDecoder = decoder;
            Console.WriteLine($"CameraTestService: Hardware decoder ready ({_previewWidth}x{_previewHeight})");

            // Notify UI that hardware decoder is ready for embedding
            OnHardwareDecoderReady?.Invoke(decoder);
        }
        else
        {
            decoder.Dispose();
            Console.WriteLine("CameraTestService: Failed to initialize hardware decoder");
            OnError?.Invoke("Failed to initialize hardware decoder");
        }
    }

    /// <summary>
    /// Stops camera test.
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("CameraTestService: Stopping...");

        _isRunning = false;
        _cts?.Cancel();

        // Wait for parser task
        if (_stderrParserTask != null)
        {
            try
            {
                await _stderrParserTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                Console.WriteLine("CameraTestService: Parser task did not stop in time");
            }
            catch (OperationCanceledException) { }
            _stderrParserTask = null;
        }

        // Wait for H.264 reader task
        if (_h264ReaderTask != null)
        {
            try
            {
                await _h264ReaderTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                Console.WriteLine("CameraTestService: H.264 reader task did not stop in time");
            }
            catch (OperationCanceledException) { }
            _h264ReaderTask = null;
        }

        // Stop capture process
        if (_captureProcess != null)
        {
            try
            {
                if (!_captureProcess.HasExited)
                {
                    _captureProcess.Kill();
                    await _captureProcess.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraTestService: Error stopping capture - {ex.Message}");
            }

            _captureProcess.Dispose();
            _captureProcess = null;
        }

        // Stop hardware decoder
        _hardwareDecoder?.Dispose();
        _hardwareDecoder = null;
        _sps = null;
        _pps = null;

        _cts?.Dispose();
        _cts = null;

        Console.WriteLine($"CameraTestService: Stopped (frames: {_frameCount})");
    }

    /// <summary>
    /// Calculates width for a given height assuming 16:9 aspect ratio.
    /// Common webcam heights: 480 → 853, 720 → 1280, 1080 → 1920
    /// </summary>
    private static int CalculateWidthFor16x9(int height)
    {
        // 16:9 aspect ratio: width = height * 16 / 9
        // Round to nearest even number for video encoding compatibility
        var width = (int)Math.Round(height * 16.0 / 9.0);
        return width % 2 == 0 ? width : width + 1;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
