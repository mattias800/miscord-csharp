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

    // Audio format info (detected from first audio frame)
    private var audioSampleRate: UInt32 = 48000
    private var audioBitsPerSample: UInt8 = 16
    private var audioChannels: UInt8 = 2
    private var audioIsFloat: Bool = false
    private var audioBytesPerSample: Int = 4  // Default: 16-bit stereo = 4 bytes
    private var audioFormatDetected: Bool = false

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

        // Find app to exclude from audio capture (e.g., Miscord.Client to avoid capturing other users' voices)
        var excludedApps: [SCRunningApplication] = []
        if let excludeAppId = config.excludeAppBundleId {
            // Try to find by bundle identifier first, then by application name
            if let appToExclude = content.applications.first(where: { $0.bundleIdentifier == excludeAppId }) {
                excludedApps.append(appToExclude)
                fputs("MiscordCapture: Will exclude audio from '\(appToExclude.applicationName)' (bundle: \(excludeAppId))\n", stderr)
            } else if let appToExclude = content.applications.first(where: { $0.applicationName == excludeAppId }) {
                excludedApps.append(appToExclude)
                fputs("MiscordCapture: Will exclude audio from '\(appToExclude.applicationName)' (by name)\n", stderr)
            } else {
                fputs("MiscordCapture: WARNING - Could not find app to exclude: '\(excludeAppId)'\n", stderr)
                fputs("MiscordCapture: Available apps: \(content.applications.map { "\($0.applicationName) (\($0.bundleIdentifier))" }.joined(separator: ", "))\n", stderr)
            }
        }

        switch config.source {
        case .display(let index):
            guard index >= 0 && index < content.displays.count else {
                throw CaptureError.sourceNotFound("Display \(index) not found")
            }
            let display = content.displays[index]
            // Capture entire display
            // Use excludingApplications only when we have apps to exclude, otherwise use simpler excludingWindows
            if excludedApps.isEmpty {
                filter = SCContentFilter(display: display, excludingWindows: [])
            } else {
                filter = SCContentFilter(display: display, excludingApplications: excludedApps, exceptingWindows: [])
            }
            fputs("MiscordCapture: Capturing display \(index) (\(display.width)x\(display.height))\n", stderr)

        case .window(let id):
            guard let window = content.windows.first(where: { $0.windowID == CGWindowID(id) }) else {
                throw CaptureError.sourceNotFound("Window \(id) not found")
            }
            // Note: Window capture doesn't support excludingApplications - audio comes from the window's app only
            filter = SCContentFilter(desktopIndependentWindow: window)
            fputs("MiscordCapture: Capturing window '\(window.title ?? "Unknown")'\n", stderr)

        case .application(let bundleId):
            guard let app = content.applications.first(where: { $0.bundleIdentifier == bundleId }) else {
                throw CaptureError.sourceNotFound("Application \(bundleId) not found")
            }
            guard let display = content.displays.first else {
                throw CaptureError.sourceNotFound("No display available")
            }
            // Capture all windows of this application (audio comes only from this app)
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
        // Use NV12 (YUV 4:2:0) - native format for H264 encoding, no conversion needed
        streamConfig.pixelFormat = kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange
        streamConfig.showsCursor = true
        fputs("MiscordCapture: Using NV12 pixel format (hardware-accelerated path)\n", stderr)

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

        // NV12 is bi-planar: Y plane (full res) + UV plane (half res interleaved)
        // Total size: width * height * 1.5 bytes
        guard CVPixelBufferGetPlaneCount(pixelBuffer) == 2 else {
            fputs("MiscordCapture: Unexpected plane count: \(CVPixelBufferGetPlaneCount(pixelBuffer))\n", stderr)
            return
        }

        // Get Y plane (plane 0)
        guard let yPlaneBase = CVPixelBufferGetBaseAddressOfPlane(pixelBuffer, 0) else { return }
        let yBytesPerRow = CVPixelBufferGetBytesPerRowOfPlane(pixelBuffer, 0)

        // Get UV plane (plane 1)
        guard let uvPlaneBase = CVPixelBufferGetBaseAddressOfPlane(pixelBuffer, 1) else { return }
        let uvBytesPerRow = CVPixelBufferGetBytesPerRowOfPlane(pixelBuffer, 1)

        // Calculate output size for packed NV12 (no padding)
        let yPlaneSize = width * height
        let uvPlaneSize = width * (height / 2)
        let totalSize = yPlaneSize + uvPlaneSize

        // Pre-allocate output buffer
        var nv12Data = Data(count: totalSize)

        nv12Data.withUnsafeMutableBytes { destBuffer in
            let destPtr = destBuffer.baseAddress!.assumingMemoryBound(to: UInt8.self)
            let yPtr = yPlaneBase.assumingMemoryBound(to: UInt8.self)
            let uvPtr = uvPlaneBase.assumingMemoryBound(to: UInt8.self)

            // Copy Y plane row by row (handles stride/padding)
            for row in 0..<height {
                let srcOffset = row * yBytesPerRow
                let destOffset = row * width
                memcpy(destPtr + destOffset, yPtr + srcOffset, width)
            }

            // Copy UV plane row by row (handles stride/padding)
            let uvDestStart = yPlaneSize
            for row in 0..<(height / 2) {
                let srcOffset = row * uvBytesPerRow
                let destOffset = uvDestStart + row * width
                memcpy(destPtr + destOffset, uvPtr + srcOffset, width)
            }
        }

        // Write NV12 data to stdout - no conversion, just pass through
        do {
            try videoOutput.write(contentsOf: nv12Data)
            frameCount += 1
            if frameCount <= 5 || frameCount % 100 == 0 {
                fputs("MiscordCapture: Video frame \(frameCount) (\(width)x\(height) NV12, \(totalSize) bytes)\n", stderr)
            }
        } catch {
            fputs("MiscordCapture: Error writing video frame: \(error)\n", stderr)
        }
    }

    private func handleAudioFrame(_ sampleBuffer: CMSampleBuffer) {
        guard let dataBuffer = sampleBuffer.dataBuffer else { return }

        // Detect audio format from the first frame
        if !audioFormatDetected {
            detectAudioFormat(from: sampleBuffer)
        }

        var length = 0
        var dataPointer: UnsafeMutablePointer<Int8>?
        CMBlockBufferGetDataPointer(dataBuffer, atOffset: 0, lengthAtOffsetOut: nil, totalLengthOut: &length, dataPointerOut: &dataPointer)

        guard let pointer = dataPointer, length > 0 else { return }

        // Get timing info
        let pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)
        let timestamp = UInt64(CMTimeGetSeconds(pts) * 1000)  // ms

        // Convert to normalized format: 48kHz 16-bit stereo
        // This handles all input formats: Float32/Int16/Int32, any sample rate, any channel count
        let normalizedData = normalizeAudio(
            pointer: pointer,
            length: length,
            isFloat: audioIsFloat,
            bitsPerSample: Int(audioBitsPerSample),
            channels: Int(audioChannels),
            inputSampleRate: Int(audioSampleRate)
        )

        guard !normalizedData.isEmpty else { return }

        // Output is always 48kHz 16-bit stereo = 4 bytes per frame
        let outputFrameCount = UInt32(normalizedData.count / 4)

        // Create header - always output as 48kHz 16-bit stereo
        let header = AudioPacketHeader(
            sampleCount: outputFrameCount,
            timestamp: timestamp,
            sampleRate: 48000,
            bitsPerSample: 16,
            channels: 2,
            isFloat: 0
        )

        // Write header + normalized audio data to stderr
        do {
            try audioOutput.write(contentsOf: header.data)
            try audioOutput.write(contentsOf: normalizedData)
            audioSampleCount += UInt64(outputFrameCount)
            if audioSampleCount <= 1000 || audioSampleCount % 48000 == 0 {
                fputs("MiscordCapture: Audio samples: \(audioSampleCount)\n", stderr)
            }
        } catch {
            fputs("MiscordCapture: Error writing audio: \(error)\n", stderr)
        }
    }

    /// Converts any input audio format to 16-bit stereo (sample rate preserved from ScreenCaptureKit)
    /// ScreenCaptureKit handles resampling via streamConfig.sampleRate = 48000
    private func normalizeAudio(pointer: UnsafeMutablePointer<Int8>, length: Int, isFloat: Bool, bitsPerSample: Int, channels: Int, inputSampleRate: Int) -> Data {
        let bytesPerSample = bitsPerSample / 8
        let bytesPerFrame = bytesPerSample * channels
        let frameCount = length / bytesPerFrame

        // Output: 16-bit stereo = 4 bytes per frame
        var output = Data(count: frameCount * 4)

        let rawPtr = UnsafeRawPointer(pointer)

        output.withUnsafeMutableBytes { outBuffer in
            let outPtr = outBuffer.baseAddress!.assumingMemoryBound(to: Int16.self)

            for i in 0..<frameCount {
                let srcByteOffset = i * bytesPerFrame

                // Read and mix all channels to stereo
                var leftSum: Float = 0
                var rightSum: Float = 0

                for ch in 0..<channels {
                    let chOffset = srcByteOffset + ch * bytesPerSample
                    var sample: Float = 0

                    if isFloat && bitsPerSample == 32 {
                        // Float32
                        sample = rawPtr.load(fromByteOffset: chOffset, as: Float.self)
                    } else if bitsPerSample == 16 {
                        // Int16
                        let intSample = rawPtr.load(fromByteOffset: chOffset, as: Int16.self)
                        sample = Float(intSample) / 32768.0
                    } else if bitsPerSample == 32 && !isFloat {
                        // Int32
                        let intSample = rawPtr.load(fromByteOffset: chOffset, as: Int32.self)
                        sample = Float(intSample) / 2147483648.0
                    } else if bitsPerSample == 24 {
                        // Int24 (packed as 3 bytes)
                        let b0 = rawPtr.load(fromByteOffset: chOffset, as: UInt8.self)
                        let b1 = rawPtr.load(fromByteOffset: chOffset + 1, as: UInt8.self)
                        let b2 = rawPtr.load(fromByteOffset: chOffset + 2, as: UInt8.self)
                        let intSample = (Int32(b2) << 24) | (Int32(b1) << 16) | (Int32(b0) << 8)
                        sample = Float(intSample) / 2147483648.0
                    }

                    // Simple stereo mapping: even channels -> left, odd channels -> right
                    if ch % 2 == 0 {
                        leftSum += sample
                    } else {
                        rightSum += sample
                    }
                }

                // Average channels per side
                let leftChannels = (channels + 1) / 2
                let rightChannels = channels / 2
                let left = leftSum / Float(max(1, leftChannels))
                let right = rightChannels > 0 ? rightSum / Float(rightChannels) : left

                // Convert to Int16 and write stereo output
                outPtr[i * 2] = Int16(clamping: Int(left * 32767))
                outPtr[i * 2 + 1] = Int16(clamping: Int(right * 32767))
            }
        }

        return output
    }

    private func detectAudioFormat(from sampleBuffer: CMSampleBuffer) {
        guard let formatDesc = CMSampleBufferGetFormatDescription(sampleBuffer) else {
            fputs("MiscordCapture: No audio format description available\n", stderr)
            return
        }

        // Get the Audio Stream Basic Description
        guard let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(formatDesc)?.pointee else {
            fputs("MiscordCapture: Could not get ASBD from format description\n", stderr)
            return
        }

        // Extract format info
        audioSampleRate = UInt32(asbd.mSampleRate)
        audioChannels = UInt8(asbd.mChannelsPerFrame)

        // Check if it's float format
        let isFloat = (asbd.mFormatFlags & kAudioFormatFlagIsFloat) != 0
        audioIsFloat = isFloat

        // Calculate bits per sample
        audioBitsPerSample = UInt8(asbd.mBitsPerChannel)

        // Calculate bytes per sample (all channels)
        audioBytesPerSample = Int(asbd.mBytesPerFrame)

        audioFormatDetected = true

        fputs("MiscordCapture: Audio format detected - \(audioSampleRate)Hz, \(audioBitsPerSample)-bit, \(audioChannels) ch, \(isFloat ? "float" : "int"), \(audioBytesPerSample) bytes/frame\n", stderr)

        // Warn if ScreenCaptureKit didn't honor our sample rate request
        if audioSampleRate != 48000 {
            fputs("MiscordCapture: WARNING - Requested 48000Hz but got \(audioSampleRate)Hz. Audio may need resampling.\n", stderr)
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
