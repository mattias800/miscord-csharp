using System.Collections.Concurrent;
using Snacka.Client.Services.HardwareVideo;
using Snacka.Shared.Models;
using SIPSorceryMedia.Abstractions;

namespace Snacka.Client.Services.WebRtc;

/// <summary>
/// Manages video decoding for incoming video streams.
/// Handles both software (FFmpeg) and hardware decoding paths.
/// Extracted from WebRtcService for single responsibility.
/// </summary>
public class VideoDecoderManager : IAsyncDisposable
{
    // Video decoders keyed by (userId, streamType) for camera and screen share
    private readonly ConcurrentDictionary<(Guid userId, VideoStreamType streamType), FfmpegProcessDecoder> _videoDecoders = new();
    // Hardware video decoders for zero-copy GPU pipeline (keyed by userId, streamType)
    private readonly ConcurrentDictionary<(Guid userId, VideoStreamType streamType), IHardwareVideoDecoder> _hardwareDecoders = new();
    // SPS/PPS storage for hardware decoder initialization (stored separately as they may arrive in different frames)
    private readonly ConcurrentDictionary<(Guid userId, VideoStreamType streamType), byte[]> _spsParams = new();
    private readonly ConcurrentDictionary<(Guid userId, VideoStreamType streamType), byte[]> _ppsParams = new();
    // Track streams where hardware decoder failed to avoid retrying every frame
    private readonly ConcurrentDictionary<(Guid userId, VideoStreamType streamType), bool> _hardwareDecoderFailed = new();

    private static bool _loggedHardwareAvailability;

    /// <summary>
    /// Fired when a video frame is decoded (RGB format). Args: (userId, streamType, width, height, rgbData)
    /// </summary>
    public event Action<Guid, VideoStreamType, int, int, byte[]>? VideoFrameReceived;

    /// <summary>
    /// Fired when an NV12 video frame is decoded (for GPU rendering). Args: (userId, streamType, width, height, nv12Data)
    /// </summary>
    public event Action<Guid, VideoStreamType, int, int, byte[]>? Nv12VideoFrameReceived;

    /// <summary>
    /// Fired when a hardware video decoder is ready for a stream. Args: (userId, streamType, decoder)
    /// The UI should embed the decoder's native view for zero-copy GPU rendering.
    /// </summary>
    public event Action<Guid, VideoStreamType, IHardwareVideoDecoder>? HardwareDecoderReady;

    /// <summary>
    /// Gets whether GPU video rendering is available on this platform.
    /// </summary>
    public bool IsGpuRenderingAvailable => Services.GpuVideo.GpuVideoRendererFactory.IsAvailable();

    /// <summary>
    /// Gets whether hardware video decoding is available on this platform.
    /// Hardware decoding provides zero-copy GPU pipeline: H264 → GPU Decode → GPU Render
    /// </summary>
    public bool IsHardwareDecodingAvailable => HardwareVideoDecoderFactory.IsAvailable();

    /// <summary>
    /// Ensures a video decoder exists for the specified user and stream type.
    /// </summary>
    public void EnsureDecoderForUser(Guid userId, Guid localUserId, VideoStreamType streamType)
    {
        // Skip creating decoder for our own streams - we don't need to decode what we're sending
        if (localUserId != Guid.Empty && userId == localUserId)
        {
            Console.WriteLine($"VideoDecoderManager: Skipping {streamType} decoder for self (user {userId})");
            return;
        }

        var key = (userId, streamType);
        if (_videoDecoders.ContainsKey(key)) return;

        try
        {
            // Use NV12 output format when GPU rendering is available (hardware-accelerated path)
            var useNv12 = IsGpuRenderingAvailable;
            var outputFormat = useNv12 ? DecoderOutputFormat.Nv12 : DecoderOutputFormat.Rgb24;

            // Use 1080p to support both camera and screen share
            var decoder = new FfmpegProcessDecoder(1920, 1080, VideoCodecsEnum.H264, outputFormat);
            decoder.OnDecodedFrame += (width, height, frameData) =>
            {
                if (useNv12)
                {
                    // Fire NV12 event for GPU rendering (fullscreen mode)
                    Nv12VideoFrameReceived?.Invoke(userId, streamType, width, height, frameData);

                    // Also convert NV12→RGB for bitmap display (tile view) if anyone is listening
                    if (VideoFrameReceived != null)
                    {
                        var rgbData = ConvertNv12ToRgb(frameData, width, height);
                        VideoFrameReceived.Invoke(userId, streamType, width, height, rgbData);
                    }
                }
                else
                {
                    // Software path - frame data is already RGB
                    VideoFrameReceived?.Invoke(userId, streamType, width, height, frameData);
                }
            };
            decoder.Start();
            _videoDecoders[key] = decoder;
            Console.WriteLine($"VideoDecoderManager: Created {streamType} video decoder for user {userId} (format={outputFormat})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VideoDecoderManager: Failed to create {streamType} video decoder for user {userId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a complete H264 frame for a user/stream.
    /// Tries hardware decoding first, falls back to software if unavailable or failed.
    /// </summary>
    public void ProcessFrame(Guid userId, VideoStreamType streamType, byte[] frame)
    {
        var key = (userId, streamType);

        // Try hardware decoder first
        if (TryProcessWithHardwareDecoder(key, frame, streamType))
        {
            return;
        }

        // Fall back to software decoder
        if (_videoDecoders.TryGetValue(key, out var decoder))
        {
            try
            {
                decoder.DecodeFrame(frame);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoDecoderManager: Error writing to software decoder: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"VideoDecoderManager: No decoder for {streamType} from user {userId}, frame dropped");
        }
    }

    /// <summary>
    /// Tries to process a complete H264 frame with the hardware decoder.
    /// Returns true if successfully processed, false to fall back to software decoder.
    /// </summary>
    private bool TryProcessWithHardwareDecoder((Guid userId, VideoStreamType streamType) key, byte[] frame, VideoStreamType streamType)
    {
        // Skip if hardware decoding not available
        if (!IsHardwareDecodingAvailable)
        {
            if (!_loggedHardwareAvailability)
            {
                _loggedHardwareAvailability = true;
                Console.WriteLine("VideoDecoderManager: Hardware decoding not available, using software decoder");
            }
            return false;
        }

        // Skip if hardware decoder already failed for this stream
        if (_hardwareDecoderFailed.ContainsKey(key))
        {
            return false;
        }

        // Parse NAL units from the Annex B frame
        var nalUnits = H264FrameAssembler.FindNalUnits(frame);
        if (nalUnits.Count == 0)
        {
            Console.WriteLine("VideoDecoderManager: Hardware decode - no NAL units found in frame");
            return false;
        }

        // Check for SPS/PPS in this frame and store them separately
        // (they may arrive in different frames from the encoder)
        foreach (var nal in nalUnits)
        {
            if (nal.Length == 0) continue;
            var nalType = nal[0] & 0x1F;
            if (nalType == 7) // SPS
            {
                _spsParams[key] = nal;
                var hex = BitConverter.ToString(nal.Take(Math.Min(16, nal.Length)).ToArray());
                Console.WriteLine($"VideoDecoderManager: Stored SPS for {streamType} ({nal.Length} bytes): {hex}");
            }
            else if (nalType == 8) // PPS
            {
                _ppsParams[key] = nal;
                var hex = BitConverter.ToString(nal.Take(Math.Min(16, nal.Length)).ToArray());
                Console.WriteLine($"VideoDecoderManager: Stored PPS for {streamType} ({nal.Length} bytes): {hex}");
            }
        }

        // Check if we have a hardware decoder for this stream
        if (!_hardwareDecoders.TryGetValue(key, out var hwDecoder))
        {
            // Try to create one if we have both SPS and PPS
            if (_spsParams.TryGetValue(key, out var sps) && _ppsParams.TryGetValue(key, out var pps))
            {
                Console.WriteLine($"VideoDecoderManager: Creating hardware decoder for {streamType}...");
                hwDecoder = HardwareVideoDecoderFactory.Create();
                if (hwDecoder != null)
                {
                    // Initialize with SPS/PPS (assuming 1920x1080 for now, could parse from SPS)
                    Console.WriteLine("VideoDecoderManager: Initializing hardware decoder with SPS/PPS...");
                    if (hwDecoder.Initialize(1920, 1080, sps, pps))
                    {
                        _hardwareDecoders[key] = hwDecoder;
                        Console.WriteLine($"VideoDecoderManager: Created hardware decoder for {streamType} (user {key.userId})");

                        // Notify listeners that hardware decoder is ready
                        HardwareDecoderReady?.Invoke(key.userId, streamType, hwDecoder);
                    }
                    else
                    {
                        hwDecoder.Dispose();
                        hwDecoder = null;
                        _hardwareDecoderFailed[key] = true;
                        Console.WriteLine($"VideoDecoderManager: Failed to initialize hardware decoder for {streamType}, will use software decoder");
                    }
                }
                else
                {
                    Console.WriteLine("VideoDecoderManager: HardwareVideoDecoderFactory.Create() returned null");
                }
            }
            else
            {
                var hasSps = _spsParams.ContainsKey(key);
                var hasPps = _ppsParams.ContainsKey(key);
                Console.WriteLine($"VideoDecoderManager: Waiting for SPS/PPS for {streamType} (have SPS={hasSps}, have PPS={hasPps})");
            }
        }

        // If we have a hardware decoder, send NAL units to it
        if (hwDecoder != null)
        {
            var nalsSent = 0;
            foreach (var nal in nalUnits)
            {
                if (nal.Length == 0) continue;
                var nalType = nal[0] & 0x1F;

                // Only send VCL NAL units (coded slice data) to the decoder
                // Type 1 = Non-IDR slice (P/B frame)
                // Type 5 = IDR slice (keyframe)
                // Skip all other NAL types (SPS=7, PPS=8, SEI=6, AUD=9, etc.)
                if (nalType != 1 && nalType != 5)
                {
                    continue;
                }

                // Determine if this is a keyframe (IDR)
                var isKeyframe = nalType == 5;

                // Decode and render
                hwDecoder.DecodeAndRender(nal, isKeyframe);
                nalsSent++;
            }
            if (nalsSent > 0)
            {
                Console.WriteLine($"VideoDecoderManager: Hardware decoded {nalsSent} NAL units for {streamType}");
            }
            return true;
        }

        Console.WriteLine($"VideoDecoderManager: No hardware decoder available for {streamType}, falling back to software");
        return false;
    }

    /// <summary>
    /// Removes the video decoder for a specific user and stream type.
    /// </summary>
    public void RemoveDecoderForUser(Guid userId, VideoStreamType streamType)
    {
        var key = (userId, streamType);

        // Remove software decoder
        if (_videoDecoders.TryRemove(key, out var decoder))
        {
            try
            {
                decoder.Dispose();
                Console.WriteLine($"VideoDecoderManager: Removed {streamType} software decoder for user {userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoDecoderManager: Error disposing software decoder: {ex.Message}");
            }
        }

        // Remove hardware decoder
        if (_hardwareDecoders.TryRemove(key, out var hwDecoder))
        {
            try
            {
                hwDecoder.Dispose();
                Console.WriteLine($"VideoDecoderManager: Removed {streamType} hardware decoder for user {userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoDecoderManager: Error disposing hardware decoder: {ex.Message}");
            }
        }

        // Remove SPS/PPS storage and failure flag
        _spsParams.TryRemove(key, out _);
        _ppsParams.TryRemove(key, out _);
        _hardwareDecoderFailed.TryRemove(key, out _);
    }

    /// <summary>
    /// Removes all video decoders for a user (both camera and screen share).
    /// </summary>
    public void RemoveAllDecodersForUser(Guid userId)
    {
        RemoveDecoderForUser(userId, VideoStreamType.Camera);
        RemoveDecoderForUser(userId, VideoStreamType.ScreenShare);
    }

    /// <summary>
    /// Clears all decoders and state.
    /// </summary>
    public void ClearAll()
    {
        foreach (var key in _videoDecoders.Keys.ToList())
        {
            RemoveDecoderForUser(key.userId, key.streamType);
        }
        _spsParams.Clear();
        _ppsParams.Clear();
        _hardwareDecoderFailed.Clear();
    }

    /// <summary>
    /// Converts NV12 (YUV 4:2:0) to RGB24 for bitmap display.
    /// </summary>
    public static byte[] ConvertNv12ToRgb(byte[] nv12Data, int width, int height)
    {
        var rgbData = new byte[width * height * 3];
        var yPlaneSize = width * height;

        // Use parallel processing for speed
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                // Y value (full resolution)
                var yIndex = y * width + x;
                var yValue = nv12Data[yIndex];

                // UV values (half resolution, interleaved)
                var uvIndex = yPlaneSize + (y / 2) * width + (x / 2) * 2;
                var uValue = nv12Data[uvIndex];
                var vValue = nv12Data[uvIndex + 1];

                // YUV to RGB conversion (BT.601)
                var c = yValue - 16;
                var d = uValue - 128;
                var e = vValue - 128;

                var r = Clamp((298 * c + 409 * e + 128) >> 8);
                var g = Clamp((298 * c - 100 * d - 208 * e + 128) >> 8);
                var b = Clamp((298 * c + 516 * d + 128) >> 8);

                var rgbIndex = (y * width + x) * 3;
                rgbData[rgbIndex] = (byte)r;
                rgbData[rgbIndex + 1] = (byte)g;
                rgbData[rgbIndex + 2] = (byte)b;
            }
        });

        return rgbData;
    }

    private static int Clamp(int value) => value < 0 ? 0 : (value > 255 ? 255 : value);

    public async ValueTask DisposeAsync()
    {
        ClearAll();
        await Task.CompletedTask;
    }
}
