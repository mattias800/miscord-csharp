# Screen Capture Protocol

This document defines the output protocol for screen capture implementations across all platforms (macOS, Windows, Linux). All platform-specific capturers MUST normalize their output to these formats to keep the shared client code simple.

## Overview

Screen capture outputs two streams:
- **Video**: Raw frames to **stdout**
- **Audio**: PCM packets with headers to **stderr**

The client (`WebRtcService.cs`) reads from these streams and expects a consistent format regardless of platform.

## Video Output (stdout)

### Format: NV12 (YUV 4:2:0)

All platforms should output **NV12** format for hardware-accelerated encoding compatibility.

| Property | Value |
|----------|-------|
| Color space | YUV 4:2:0 (NV12) |
| Byte order | Y plane first, then interleaved UV plane |
| Size per frame | `width * height * 1.5` bytes |

**NV12 Layout:**
```
[Y plane: width * height bytes]
[UV plane: width * height / 2 bytes, interleaved U0,V0,U1,V1,...]
```

**Example:** 1920x1080 frame = 1920 * 1080 * 1.5 = 3,110,400 bytes

### Fallback: BGR24

For FFmpeg-based capture (legacy), BGR24 is also supported:

| Property | Value |
|----------|-------|
| Color space | BGR24 |
| Byte order | B, G, R per pixel |
| Size per frame | `width * height * 3` bytes |

## Audio Output (stderr)

### Normalized Format

**All platform capturers MUST normalize audio to this format before output:**

| Property | Value |
|----------|-------|
| Sample rate | **48000 Hz** |
| Bit depth | **16-bit signed integer** |
| Channels | **2 (stereo)** |
| Layout | **Interleaved** (L0, R0, L1, R1, ...) |
| Byte order | **Little-endian** |

This normalization happens at the capture stage, NOT in the client. The client code assumes all audio arrives in this exact format.

### Why Normalize at Capture Stage?

1. **Platform APIs return different formats:**
   - macOS ScreenCaptureKit: 32-bit float, planar (non-interleaved), variable sample rate
   - Windows WASAPI: 32-bit float, interleaved, device-dependent sample rate
   - Linux PulseAudio: 16-bit or 32-bit, interleaved, device-dependent sample rate

2. **Single client implementation:** The client (`ProcessScreenShareAudio`) only handles one format
3. **Reduced complexity:** No format detection or conversion in shared C# code
4. **Consistent behavior:** Same audio quality across all platforms

### Packet Header Format

Each audio packet is prefixed with a 24-byte header:

```
Offset  Size  Field           Description
------  ----  -----           -----------
0       4     Magic           0x4D434150 ("MCAP" in ASCII, little-endian)
4       1     Version         Protocol version (currently 2)
5       1     BitsPerSample   Always 16 for normalized output
6       1     Channels        Always 2 for normalized output
7       1     IsFloat         Always 0 for normalized output (integer PCM)
8       4     SampleCount     Number of stereo frames in this packet
12      4     SampleRate      Always 48000 for normalized output
16      8     Timestamp       Presentation timestamp in milliseconds
------
Total: 24 bytes
```

### Packet Structure

```
[Header: 24 bytes][PCM Data: SampleCount * 4 bytes]
```

Each stereo frame is 4 bytes: 2 bytes (left) + 2 bytes (right).

**Example:** 960 frames (20ms at 48kHz) = 24 + (960 * 4) = 3,864 bytes total

### C Structure (for reference)

```c
typedef struct {
    uint32_t magic;         // 0x4D434150 "MCAP"
    uint8_t  version;       // 2
    uint8_t  bitsPerSample; // 16
    uint8_t  channels;      // 2
    uint8_t  isFloat;       // 0
    uint32_t sampleCount;   // frames in packet
    uint32_t sampleRate;    // 48000
    uint64_t timestamp;     // milliseconds
} AudioPacketHeader;        // 24 bytes, packed
```

## Implementation Requirements

### macOS (SnackaCapture - Swift)

**Current implementation:** `src/SnackaCapture/Sources/SnackaCapture/ScreenCapturer.swift`

- Uses ScreenCaptureKit (macOS 13+)
- Detects input format from AudioStreamBasicDescription (ASBD)
- Handles Float32, Int16, Int24, Int32 input formats
- Handles both interleaved and planar (non-interleaved) audio
- Resamples if input is not 48kHz
- Outputs normalized 48kHz 16-bit stereo interleaved

### Windows (To Be Implemented)

Recommended approach:
1. Use WASAPI loopback for system audio capture
2. Native audio is typically 32-bit float, device sample rate
3. **Must convert to:** 48kHz 16-bit stereo interleaved
4. Use same header format (MCAP magic, version 2)

Example conversion steps:
```
WASAPI (32-bit float, 44.1/48/96kHz)
  → Resample to 48kHz if needed
  → Convert float [-1.0, 1.0] to int16 [-32768, 32767]
  → Output with MCAP header to stderr
```

### Linux (To Be Implemented)

Recommended approach:
1. Use PulseAudio monitor source for system audio capture
2. Native format varies by configuration
3. **Must convert to:** 48kHz 16-bit stereo interleaved
4. Use same header format (MCAP magic, version 2)

## Client-Side Processing

The client (`WebRtcService.cs`) expects:

1. **Video:** Read exact frame size from stdout based on resolution
2. **Audio:**
   - Scan stderr for MCAP magic (0x4D434150)
   - Read 24-byte header
   - Read `sampleCount * 4` bytes of PCM data
   - Encode to Opus stereo and send via RTP

The client does NOT perform any audio format conversion - it trusts the capturer to provide normalized 48kHz 16-bit stereo.

## Testing Checklist

When implementing a new platform capturer, verify:

- [ ] Audio header starts with magic 0x4D434150
- [ ] Header version is 2
- [ ] BitsPerSample is 16
- [ ] Channels is 2
- [ ] IsFloat is 0
- [ ] SampleRate is 48000
- [ ] PCM data is interleaved stereo (L0,R0,L1,R1,...)
- [ ] PCM samples are 16-bit signed little-endian
- [ ] Audio sounds correct (not pitched up/down, not corrupted)
- [ ] Video frames are NV12 format at correct resolution
