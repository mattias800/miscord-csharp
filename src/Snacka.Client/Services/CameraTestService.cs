using System.Diagnostics;
using Snacka.Client.Services.WebRtc;

namespace Snacka.Client.Services;

/// <summary>
/// Service for testing camera with dual preview (raw capture + H.264 encoded).
/// Used in video settings to show users what their camera looks like and
/// how the encoded stream differs from the raw capture.
/// </summary>
public class CameraTestService : IDisposable
{
    private readonly NativeCaptureLocator _captureLocator;
    private Process? _captureProcess;
    private FfmpegProcessDecoder? _h264Decoder;
    private Task? _stderrParserTask;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    private int _rawFrameCount;
    private int _encodedFrameCount;

    /// <summary>
    /// Fired when a raw preview frame is received (NV12 format for GPU rendering).
    /// Parameters: width, height, nv12Data
    /// </summary>
    public event Action<int, int, byte[]>? OnRawNv12FrameReceived;

    /// <summary>
    /// Fired when a decoded H.264 frame is received (NV12 format for GPU rendering).
    /// Parameters: width, height, nv12Data
    /// </summary>
    public event Action<int, int, byte[]>? OnEncodedNv12FrameReceived;

    /// <summary>
    /// Fired when an error occurs during capture/decode.
    /// </summary>
    public event Action<string>? OnError;

    public bool IsRunning => _isRunning;
    public int RawFrameCount => _rawFrameCount;
    public int EncodedFrameCount => _encodedFrameCount;

    public CameraTestService()
    {
        _captureLocator = new NativeCaptureLocator();
    }

    /// <summary>
    /// Starts camera test with dual preview output.
    /// </summary>
    /// <param name="cameraId">Camera device ID or index</param>
    /// <param name="resolution">Resolution string like "640x480"</param>
    /// <param name="fps">Frame rate</param>
    /// <param name="bitrateMbps">Encoding bitrate in Mbps</param>
    public async Task StartAsync(string cameraId, string resolution, int fps, int bitrateMbps)
    {
        if (_isRunning)
        {
            await StopAsync();
        }

        _rawFrameCount = 0;
        _encodedFrameCount = 0;
        _cts = new CancellationTokenSource();

        // Parse resolution
        var parts = resolution.Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height))
        {
            OnError?.Invoke($"Invalid resolution format: {resolution}");
            return;
        }

        // Get native capture tool path
        var capturePath = _captureLocator.GetNativeCameraCapturePath();
        if (capturePath == null)
        {
            OnError?.Invoke("Native capture tool not found. Ensure SnackaCaptureVideoToolbox (macOS), SnackaCaptureWindows, or SnackaCaptureLinux is built.");
            return;
        }

        // Build arguments with preview enabled
        var args = _captureLocator.GetNativeCameraCaptureArgs(cameraId, width, height, fps, bitrateMbps, outputPreview: true, previewFps: 15);

        Console.WriteLine($"CameraTestService: Starting capture: {capturePath} {args}");

        try
        {
            // Start H.264 decoder first (with NV12 output for GPU rendering)
            _h264Decoder = new FfmpegProcessDecoder(width, height, outputFormat: DecoderOutputFormat.Nv12);
            _h264Decoder.OnDecodedFrame += OnH264FrameDecoded;
            _h264Decoder.Start();

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

            // Start parsing stderr for preview frames
            var token = _cts.Token;
            _stderrParserTask = Task.Run(() => ParseStderrLoop(width, height, token), token);

            // Start piping stdout (H.264) to decoder
            _ = Task.Run(() => PipeH264ToDecoder(token), token);

            Console.WriteLine($"CameraTestService: Capture started, pid={_captureProcess.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CameraTestService: Failed to start - {ex.Message}");
            OnError?.Invoke($"Failed to start capture: {ex.Message}");
            await StopAsync();
        }
    }

    private void ParseStderrLoop(int expectedWidth, int expectedHeight, CancellationToken token)
    {
        if (_captureProcess == null) return;

        try
        {
            var parser = new StderrPacketParser(_captureProcess.StandardError.BaseStream);

            parser.OnPreviewPacket += packet =>
            {
                _rawFrameCount++;

                if (_rawFrameCount <= 5 || _rawFrameCount % 100 == 0)
                {
                    Console.WriteLine($"CameraTestService: Raw preview frame {_rawFrameCount}, {packet.Width}x{packet.Height}");
                }

                // Fire NV12 event directly (no CPU conversion - GPU will handle YUV→RGB)
                OnRawNv12FrameReceived?.Invoke(packet.Width, packet.Height, packet.PixelData);
            };

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

    private async Task PipeH264ToDecoder(CancellationToken token)
    {
        if (_captureProcess == null || _h264Decoder == null) return;

        try
        {
            var stdout = _captureProcess.StandardOutput.BaseStream;
            var buffer = new byte[64 * 1024];
            long totalBytes = 0;

            while (!token.IsCancellationRequested)
            {
                var bytesRead = await stdout.ReadAsync(buffer, 0, buffer.Length, token);
                if (bytesRead == 0)
                {
                    Console.WriteLine("CameraTestService: H.264 stream ended");
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes <= 10000 || totalBytes % 100000 < bytesRead)
                {
                    Console.WriteLine($"CameraTestService: Piped {bytesRead} H.264 bytes to decoder (total: {totalBytes})");
                }

                // Copy data and send to decoder
                var frameData = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, frameData, 0, bytesRead);
                _h264Decoder.DecodeFrame(frameData);
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
                Console.WriteLine($"CameraTestService: H.264 pipe error - {ex.Message}");
            }
        }
    }

    private void OnH264FrameDecoded(int width, int height, byte[] nv12Data)
    {
        _encodedFrameCount++;

        if (_encodedFrameCount <= 5 || _encodedFrameCount % 100 == 0)
        {
            Console.WriteLine($"CameraTestService: Decoded H.264 frame {_encodedFrameCount}, {width}x{height}");
        }

        // Fire NV12 event directly (GPU will handle YUV→RGB)
        OnEncodedNv12FrameReceived?.Invoke(width, height, nv12Data);
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
        }

        // Stop decoder
        _h264Decoder?.Dispose();
        _h264Decoder = null;

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

        _cts?.Dispose();
        _cts = null;

        Console.WriteLine($"CameraTestService: Stopped (raw frames: {_rawFrameCount}, encoded frames: {_encodedFrameCount})");
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
