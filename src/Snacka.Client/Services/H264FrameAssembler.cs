namespace Snacka.Client.Services;

/// <summary>
/// Reassembles H264 frames from RTP packets.
/// Handles FU-A fragmentation and STAP-A aggregation.
/// Waits for a keyframe before returning any frames to the decoder.
/// </summary>
public class H264FrameAssembler
{
    private readonly List<byte> _frameBuffer = new();
    private readonly List<byte> _fuaBuffer = new(); // For FU-A reassembly
    private uint _currentTimestamp;
    private bool _hasFrame;
    private bool _hasReceivedKeyframe;
    private int _droppedFrameCount;

    /// <summary>
    /// Processes an RTP packet payload. Returns a complete frame when marker bit indicates end of frame.
    /// Only returns frames after a keyframe (SPS/PPS or IDR) has been received.
    /// </summary>
    public byte[]? ProcessPacket(byte[] payload, uint timestamp, bool markerBit)
    {
        if (payload.Length == 0) return null;

        // If timestamp changed, reset for new frame
        if (timestamp != _currentTimestamp)
        {
            _frameBuffer.Clear();
            _fuaBuffer.Clear();
            _currentTimestamp = timestamp;
            _hasFrame = false;
        }

        // Get NAL unit type from first byte
        byte firstByte = payload[0];
        int nalType = firstByte & 0x1F;

        // Check for keyframe NAL types:
        // 5 = IDR (Instantaneous Decoder Refresh - keyframe)
        // 7 = SPS (Sequence Parameter Set)
        // 8 = PPS (Picture Parameter Set)
        if (nalType == 5 || nalType == 7 || nalType == 8)
        {
            if (!_hasReceivedKeyframe)
            {
                Console.WriteLine($"H264FrameAssembler: Received keyframe (NAL type {nalType}), dropped {_droppedFrameCount} frames while waiting");
            }
            _hasReceivedKeyframe = true;
        }
        // Also check FU-A fragments for IDR frames
        else if (nalType == 28 && payload.Length >= 2)
        {
            int fuaNalType = payload[1] & 0x1F;
            if (fuaNalType == 5 || fuaNalType == 7 || fuaNalType == 8)
            {
                if (!_hasReceivedKeyframe)
                {
                    Console.WriteLine($"H264FrameAssembler: Received keyframe in FU-A (NAL type {fuaNalType}), dropped {_droppedFrameCount} frames while waiting");
                }
                _hasReceivedKeyframe = true;
            }
        }

        if (nalType >= 1 && nalType <= 23)
        {
            // Single NAL unit packet - add with start code
            AddNalUnit(payload);
        }
        else if (nalType == 28) // FU-A
        {
            ProcessFuA(payload);
        }
        else if (nalType == 24) // STAP-A
        {
            ProcessStapA(payload);
        }
        // Other types (FU-B, STAP-B, etc.) are rare, skip them

        _hasFrame = true;

        // If marker bit is set, frame is complete
        if (markerBit && _hasFrame && _frameBuffer.Count > 0)
        {
            var frame = _frameBuffer.ToArray();
            _frameBuffer.Clear();
            _hasFrame = false;

            // Only return frames after we've received a keyframe
            if (_hasReceivedKeyframe)
            {
                return frame;
            }
            else
            {
                _droppedFrameCount++;
                return null;
            }
        }

        return null;
    }

    private void AddNalUnit(byte[] nalUnit)
    {
        // Add Annex B start code (0x00 0x00 0x00 0x01)
        _frameBuffer.Add(0x00);
        _frameBuffer.Add(0x00);
        _frameBuffer.Add(0x00);
        _frameBuffer.Add(0x01);
        _frameBuffer.AddRange(nalUnit);
    }

    private void ProcessFuA(byte[] payload)
    {
        if (payload.Length < 2) return;

        byte fuIndicator = payload[0];
        byte fuHeader = payload[1];

        bool startBit = (fuHeader & 0x80) != 0;
        bool endBit = (fuHeader & 0x40) != 0;
        int nalType = fuHeader & 0x1F;

        if (startBit)
        {
            // Start of fragmented NAL unit - reconstruct NAL header
            _fuaBuffer.Clear();
            byte nalHeader = (byte)((fuIndicator & 0xE0) | nalType);
            _fuaBuffer.Add(nalHeader);
        }

        // Add fragment payload (skip FU indicator and FU header)
        for (int i = 2; i < payload.Length; i++)
        {
            _fuaBuffer.Add(payload[i]);
        }

        if (endBit)
        {
            // End of fragmented NAL unit - add complete NAL to frame
            AddNalUnit(_fuaBuffer.ToArray());
            _fuaBuffer.Clear();
        }
    }

    private void ProcessStapA(byte[] payload)
    {
        // Skip STAP-A header byte
        int offset = 1;

        while (offset + 2 < payload.Length)
        {
            // Read NAL unit size (2 bytes, big endian)
            int nalSize = (payload[offset] << 8) | payload[offset + 1];
            offset += 2;

            if (offset + nalSize > payload.Length) break;

            // Extract NAL unit
            var nalUnit = new byte[nalSize];
            Array.Copy(payload, offset, nalUnit, 0, nalSize);
            AddNalUnit(nalUnit);

            offset += nalSize;
        }
    }

    public void Reset()
    {
        _frameBuffer.Clear();
        _fuaBuffer.Clear();
        _hasFrame = false;
        _hasReceivedKeyframe = false;
        _droppedFrameCount = 0;
    }

    #region Static NAL Unit Parsing Utilities

    /// <summary>
    /// Finds H264 NAL units in a byte stream (Annex B format).
    /// Used for packetization when sending video.
    /// </summary>
    public static List<byte[]> FindNalUnits(byte[] data)
    {
        var nalUnits = new List<byte[]>();
        var startIndices = new List<int>();

        // Find all start code positions (00 00 01 or 00 00 00 01)
        for (int i = 0; i < data.Length - 3; i++)
        {
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1)
                {
                    startIndices.Add(i + 3); // 3-byte start code
                }
                else if (i < data.Length - 4 && data[i + 2] == 0 && data[i + 3] == 1)
                {
                    startIndices.Add(i + 4); // 4-byte start code
                    i++; // Skip extra byte
                }
            }
        }

        // Extract NAL units between start codes
        for (int i = 0; i < startIndices.Count; i++)
        {
            int start = startIndices[i];
            int end = (i + 1 < startIndices.Count) ? FindStartCodeBefore(data, startIndices[i + 1]) : data.Length;
            int length = end - start;

            if (length > 0)
            {
                var nalUnit = new byte[length];
                Array.Copy(data, start, nalUnit, 0, length);
                nalUnits.Add(nalUnit);
            }
        }

        // If no start codes found, treat entire buffer as single NAL unit
        if (nalUnits.Count == 0 && data.Length > 0)
        {
            nalUnits.Add(data);
        }

        return nalUnits;
    }

    /// <summary>
    /// Finds the start of the start code before the given position.
    /// </summary>
    private static int FindStartCodeBefore(byte[] data, int position)
    {
        // Check for 4-byte start code
        if (position >= 4 && data[position - 4] == 0 && data[position - 3] == 0 &&
            data[position - 2] == 0 && data[position - 1] == 1)
        {
            return position - 4;
        }
        // Check for 3-byte start code
        if (position >= 3 && data[position - 3] == 0 && data[position - 2] == 0 && data[position - 1] == 1)
        {
            return position - 3;
        }
        return position;
    }

    #endregion
}
