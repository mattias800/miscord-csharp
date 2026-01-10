import Foundation

// MARK: - Source Listing Models

struct AvailableSources: Codable {
    let displays: [DisplaySource]
    let windows: [WindowSource]
    let applications: [ApplicationSource]
}

struct DisplaySource: Codable {
    let id: String
    let name: String
    let width: Int
    let height: Int
}

struct WindowSource: Codable {
    let id: String
    let name: String
    let appName: String
    let bundleId: String?
}

struct ApplicationSource: Codable {
    let bundleId: String
    let name: String
}

// MARK: - Capture Configuration

enum CaptureSourceType {
    case display(index: Int)
    case window(id: Int)
    case application(bundleId: String)
}

struct CaptureConfig {
    let source: CaptureSourceType
    let width: Int
    let height: Int
    let fps: Int
    let captureAudio: Bool
    let excludeCurrentProcessAudio: Bool
}

// MARK: - Output Protocol

/// Protocol marker for video/audio output streams
/// Video: BGR24 raw frames to stdout
/// Audio: PCM S16LE 48kHz stereo to stderr (interleaved with log messages using a header)
struct AudioPacketHeader {
    static let magic: UInt32 = 0x4D434150  // "MCAP" in ASCII
    let sampleCount: UInt32
    let timestamp: UInt64

    var data: Data {
        var header = Data()
        var magic = Self.magic
        var samples = sampleCount
        var ts = timestamp
        header.append(Data(bytes: &magic, count: 4))
        header.append(Data(bytes: &samples, count: 4))
        header.append(Data(bytes: &ts, count: 8))
        return header
    }

    static let size = 16
}
