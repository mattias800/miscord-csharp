import Foundation
import ScreenCaptureKit
import CoreMedia
import CoreVideo
import AVFoundation

/// Main screen capture class using ScreenCaptureKit
class ScreenCapturer: NSObject, SCStreamDelegate, SCStreamOutput {
    private let config: CaptureConfig
    private var stream: SCStream?
    private var isRunning = false
    private let videoQueue = DispatchQueue(label: "com.miscord.capture.video", qos: .userInteractive)
    private let audioQueue = DispatchQueue(label: "com.miscord.capture.audio", qos: .userInteractive)

    // Output handles
    private let videoOutput = FileHandle.standardOutput
    private let audioOutput = FileHandle.standardError

    // Frame timing
    private var frameCount: UInt64 = 0
    private var audioSampleCount: UInt64 = 0

    // Continuation for keeping the process alive
    private var runContinuation: CheckedContinuation<Void, Never>?

    init(config: CaptureConfig) {
        self.config = config
        super.init()
    }

    func start() async throws {
        // Create content filter based on source type
        let filter: SCContentFilter
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)

        switch config.source {
        case .display(let index):
            guard index >= 0 && index < content.displays.count else {
                throw CaptureError.sourceNotFound("Display \(index) not found")
            }
            let display = content.displays[index]
            // Capture entire display, excluding nothing
            filter = SCContentFilter(display: display, excludingWindows: [])
            fputs("MiscordCapture: Capturing display \(index) (\(display.width)x\(display.height))\n", stderr)

        case .window(let id):
            guard let window = content.windows.first(where: { $0.windowID == CGWindowID(id) }) else {
                throw CaptureError.sourceNotFound("Window \(id) not found")
            }
            filter = SCContentFilter(desktopIndependentWindow: window)
            fputs("MiscordCapture: Capturing window '\(window.title ?? "Unknown")'\n", stderr)

        case .application(let bundleId):
            guard let app = content.applications.first(where: { $0.bundleIdentifier == bundleId }) else {
                throw CaptureError.sourceNotFound("Application \(bundleId) not found")
            }
            guard let display = content.displays.first else {
                throw CaptureError.sourceNotFound("No display available")
            }
            // Capture all windows of this application
            let appWindows = content.windows.filter { $0.owningApplication?.bundleIdentifier == bundleId }
            filter = SCContentFilter(display: display, including: [app], exceptingWindows: [])
            fputs("MiscordCapture: Capturing application '\(app.applicationName)' (\(appWindows.count) windows)\n", stderr)
        }

        // Configure stream
        let streamConfig = SCStreamConfiguration()

        // Video settings
        streamConfig.width = config.width
        streamConfig.height = config.height
        streamConfig.minimumFrameInterval = CMTime(value: 1, timescale: CMTimeScale(config.fps))
        streamConfig.pixelFormat = kCVPixelFormatType_32BGRA  // We'll convert to BGR24
        streamConfig.showsCursor = true

        // Audio settings
        if config.captureAudio {
            streamConfig.capturesAudio = true
            streamConfig.excludesCurrentProcessAudio = config.excludeCurrentProcessAudio
            streamConfig.sampleRate = 48000
            streamConfig.channelCount = 2
            fputs("MiscordCapture: Audio capture enabled (48kHz stereo)\n", stderr)
        }

        // Create and start stream
        stream = SCStream(filter: filter, configuration: streamConfig, delegate: self)

        try stream?.addStreamOutput(self, type: .screen, sampleHandlerQueue: videoQueue)
        if config.captureAudio {
            try stream?.addStreamOutput(self, type: .audio, sampleHandlerQueue: audioQueue)
        }

        try await stream?.startCapture()
        isRunning = true
        fputs("MiscordCapture: Capture started\n", stderr)

        // Handle termination signals
        setupSignalHandlers()
    }

    func stop() async {
        guard isRunning else { return }
        isRunning = false

        try? await stream?.stopCapture()
        stream = nil

        fputs("MiscordCapture: Capture stopped (frames: \(frameCount), audio samples: \(audioSampleCount))\n", stderr)
        runContinuation?.resume()
    }

    func waitUntilDone() async {
        await withCheckedContinuation { continuation in
            runContinuation = continuation
        }
    }

    // MARK: - SCStreamDelegate

    func stream(_ stream: SCStream, didStopWithError error: Error) {
        fputs("MiscordCapture: Stream stopped with error: \(error.localizedDescription)\n", stderr)
        Task {
            await stop()
        }
    }

    // MARK: - SCStreamOutput

    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard isRunning else { return }

        switch type {
        case .screen:
            handleVideoFrame(sampleBuffer)
        case .audio:
            handleAudioFrame(sampleBuffer)
        case .microphone:
            // We don't capture microphone through ScreenCaptureKit
            break
        @unknown default:
            break
        }
    }

    // MARK: - Frame Handling

    private func handleVideoFrame(_ sampleBuffer: CMSampleBuffer) {
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }

        // Lock the pixel buffer
        CVPixelBufferLockBaseAddress(pixelBuffer, .readOnly)
        defer { CVPixelBufferUnlockBaseAddress(pixelBuffer, .readOnly) }

        let width = CVPixelBufferGetWidth(pixelBuffer)
        let height = CVPixelBufferGetHeight(pixelBuffer)
        let bytesPerRow = CVPixelBufferGetBytesPerRow(pixelBuffer)

        guard let baseAddress = CVPixelBufferGetBaseAddress(pixelBuffer) else { return }

        // Convert BGRA to BGR24
        // Input: BGRA (4 bytes per pixel)
        // Output: BGR (3 bytes per pixel)
        let outputSize = config.width * config.height * 3
        var bgrData = Data(capacity: outputSize)

        let srcPtr = baseAddress.assumingMemoryBound(to: UInt8.self)

        // Handle potential scaling (if capture size differs from requested size)
        // For now, assume sizes match or we need simple conversion
        for y in 0..<min(height, config.height) {
            for x in 0..<min(width, config.width) {
                let srcOffset = y * bytesPerRow + x * 4
                // BGRA -> BGR (skip alpha)
                bgrData.append(srcPtr[srcOffset])     // B
                bgrData.append(srcPtr[srcOffset + 1]) // G
                bgrData.append(srcPtr[srcOffset + 2]) // R
            }
            // Pad row if needed
            if width < config.width {
                let padding = (config.width - width) * 3
                bgrData.append(contentsOf: [UInt8](repeating: 0, count: padding))
            }
        }

        // Pad remaining rows if needed
        if height < config.height {
            let remainingRows = config.height - height
            let rowSize = config.width * 3
            bgrData.append(contentsOf: [UInt8](repeating: 0, count: remainingRows * rowSize))
        }

        // Write to stdout
        do {
            try videoOutput.write(contentsOf: bgrData)
            frameCount += 1
            if frameCount <= 5 || frameCount % 100 == 0 {
                fputs("MiscordCapture: Video frame \(frameCount) (\(width)x\(height) -> \(config.width)x\(config.height))\n", stderr)
            }
        } catch {
            fputs("MiscordCapture: Error writing video frame: \(error)\n", stderr)
        }
    }

    private func handleAudioFrame(_ sampleBuffer: CMSampleBuffer) {
        guard let dataBuffer = sampleBuffer.dataBuffer else { return }

        var length = 0
        var dataPointer: UnsafeMutablePointer<Int8>?
        CMBlockBufferGetDataPointer(dataBuffer, atOffset: 0, lengthAtOffsetOut: nil, totalLengthOut: &length, dataPointerOut: &dataPointer)

        guard let pointer = dataPointer, length > 0 else { return }

        // Get timing info
        let pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)
        let timestamp = UInt64(CMTimeGetSeconds(pts) * 1000)  // ms

        // Calculate sample count (16-bit stereo = 4 bytes per sample)
        let sampleCount = UInt32(length / 4)

        // Create header
        let header = AudioPacketHeader(sampleCount: sampleCount, timestamp: timestamp)

        // Write header + audio data to stderr
        // Note: We use a magic number to distinguish audio data from log messages
        do {
            try audioOutput.write(contentsOf: header.data)
            try audioOutput.write(contentsOf: Data(bytes: pointer, count: length))
            audioSampleCount += UInt64(sampleCount)
            if audioSampleCount <= 1000 || audioSampleCount % 48000 == 0 {
                fputs("MiscordCapture: Audio samples: \(audioSampleCount)\n", stderr)
            }
        } catch {
            fputs("MiscordCapture: Error writing audio: \(error)\n", stderr)
        }
    }

    // MARK: - Signal Handling

    private func setupSignalHandlers() {
        signal(SIGINT) { _ in
            fputs("\nMiscordCapture: Received SIGINT\n", stderr)
            exit(0)
        }
        signal(SIGTERM) { _ in
            fputs("\nMiscordCapture: Received SIGTERM\n", stderr)
            exit(0)
        }
    }
}
