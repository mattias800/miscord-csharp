#include "Protocol.h"
#include "SourceLister.h"
#include "DisplayCapturer.h"
#include "WindowCapturer.h"
#include "AudioCapturer.h"

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>

#include <iostream>
#include <string>
#include <vector>
#include <thread>
#include <atomic>
#include <io.h>
#include <fcntl.h>

using namespace snacka;

// Global flag for clean shutdown
std::atomic<bool> g_running{true};

// Console control handler
BOOL WINAPI ConsoleHandler(DWORD signal) {
    if (signal == CTRL_C_EVENT || signal == CTRL_BREAK_EVENT || signal == CTRL_CLOSE_EVENT) {
        std::cerr << "\nSnackaCaptureWindows: Received shutdown signal\n";
        g_running = false;
        return TRUE;
    }
    return FALSE;
}

void PrintUsage() {
    std::cerr << R"(
SnackaCaptureWindows - Screen and audio capture tool for Windows

USAGE:
    SnackaCaptureWindows list [--json]
    SnackaCaptureWindows [OPTIONS]

COMMANDS:
    list              List available capture sources

OPTIONS:
    --display <index>   Display index to capture (default: 0)
    --window <hwnd>     Window handle to capture
    --width <pixels>    Output width (default: 1920)
    --height <pixels>   Output height (default: 1080)
    --fps <rate>        Frames per second (default: 30)
    --audio             Capture system audio
    --json              Output source list as JSON (with 'list' command)
    --help              Show this help message

EXAMPLES:
    SnackaCaptureWindows list --json
    SnackaCaptureWindows --display 0 --width 1920 --height 1080 --fps 30
    SnackaCaptureWindows --window 12345678 --audio
)";
}

int ListSources(bool asJson) {
    auto sources = SourceLister::GetAvailableSources();

    if (asJson) {
        SourceLister::PrintSourcesAsJson(sources);
    } else {
        SourceLister::PrintSources(sources);
    }

    return 0;
}

int Capture(int displayIndex, HWND windowHandle, int width, int height, int fps, bool captureAudio) {
    // Set stdout to binary mode for raw frame output
    _setmode(_fileno(stdout), _O_BINARY);
    _setmode(_fileno(stderr), _O_BINARY);

    // Set up console handler for clean shutdown
    SetConsoleCtrlHandler(ConsoleHandler, TRUE);

    // Initialize COM for audio
    CoInitializeEx(nullptr, COINIT_MULTITHREADED);

    std::cerr << "SnackaCaptureWindows: Starting capture "
              << width << "x" << height << " @ " << fps << "fps"
              << (captureAudio ? ", audio=true" : ", audio=false") << "\n";

    // Frame and audio statistics
    uint64_t frameCount = 0;
    uint64_t audioPacketCount = 0;

    // Write video frames to stdout
    auto videoCallback = [&](const uint8_t* data, size_t size, uint64_t timestamp) {
        if (!g_running) return;

        size_t written = 0;
        while (written < size && g_running) {
            int result = _write(_fileno(stdout), data + written, static_cast<unsigned int>(size - written));
            if (result < 0) {
                std::cerr << "SnackaCaptureWindows: Error writing video frame\n";
                g_running = false;
                return;
            }
            written += result;
        }

        frameCount++;
        if (frameCount <= 5 || frameCount % 100 == 0) {
            std::cerr << "SnackaCaptureWindows: Video frame " << frameCount
                      << " (" << width << "x" << height << " NV12, " << size << " bytes)\n";
        }
    };

    // Write audio packets to stderr
    auto audioCallback = [&](const uint8_t* data, size_t size, uint64_t timestamp) {
        if (!g_running) return;

        // Audio packets include the header, write directly to stderr
        // Note: We use a separate file descriptor to avoid mixing with log messages
        size_t written = 0;
        while (written < size && g_running) {
            int result = _write(_fileno(stderr), data + written, static_cast<unsigned int>(size - written));
            if (result < 0) {
                g_running = false;
                return;
            }
            written += result;
        }

        audioPacketCount++;
        if (audioPacketCount <= 10 || audioPacketCount % 100 == 0) {
            // Log to a file instead of stderr to avoid mixing with audio data
            // For now, skip logging audio stats
        }
    };

    // Start audio capture if requested
    std::unique_ptr<AudioCapturer> audioCapturer;
    if (captureAudio) {
        audioCapturer = std::make_unique<AudioCapturer>();
        if (!audioCapturer->Initialize()) {
            std::cerr << "SnackaCaptureWindows: WARNING - Failed to initialize audio capture\n";
            audioCapturer.reset();
        } else {
            audioCapturer->Start(audioCallback);
        }
    }

    // Start video capture
    bool captureStarted = false;

    if (windowHandle != nullptr) {
        // Window capture
        auto capturer = std::make_unique<WindowCapturer>();
        if (capturer->Initialize(windowHandle, width, height, fps)) {
            capturer->Start(videoCallback);
            captureStarted = true;

            // Wait for shutdown
            while (g_running && capturer->IsRunning()) {
                Sleep(100);
            }

            capturer->Stop();
        }
    } else {
        // Display capture
        auto capturer = std::make_unique<DisplayCapturer>();
        if (capturer->Initialize(displayIndex, width, height, fps)) {
            capturer->Start(videoCallback);
            captureStarted = true;

            // Wait for shutdown
            while (g_running && capturer->IsRunning()) {
                Sleep(100);
            }

            capturer->Stop();
        }
    }

    // Stop audio capture
    if (audioCapturer) {
        audioCapturer->Stop();
    }

    if (!captureStarted) {
        std::cerr << "SnackaCaptureWindows: Failed to start capture\n";
        CoUninitialize();
        return 1;
    }

    std::cerr << "SnackaCaptureWindows: Capture stopped (frames: " << frameCount
              << ", audio packets: " << audioPacketCount << ")\n";

    CoUninitialize();
    return 0;
}

int main(int argc, char* argv[]) {
    // Parse command line arguments
    std::vector<std::string> args(argv, argv + argc);

    // Check for help
    for (const auto& arg : args) {
        if (arg == "--help" || arg == "-h") {
            PrintUsage();
            return 0;
        }
    }

    // Check for 'list' command
    if (args.size() >= 2 && args[1] == "list") {
        bool asJson = false;
        for (size_t i = 2; i < args.size(); i++) {
            if (args[i] == "--json") {
                asJson = true;
            }
        }
        return ListSources(asJson);
    }

    // Parse capture options
    int displayIndex = 0;
    HWND windowHandle = nullptr;
    int width = 1920;
    int height = 1080;
    int fps = 30;
    bool captureAudio = false;

    for (size_t i = 1; i < args.size(); i++) {
        if (args[i] == "--display" && i + 1 < args.size()) {
            displayIndex = std::stoi(args[++i]);
        } else if (args[i] == "--window" && i + 1 < args.size()) {
            windowHandle = reinterpret_cast<HWND>(std::stoull(args[++i]));
        } else if (args[i] == "--width" && i + 1 < args.size()) {
            width = std::stoi(args[++i]);
        } else if (args[i] == "--height" && i + 1 < args.size()) {
            height = std::stoi(args[++i]);
        } else if (args[i] == "--fps" && i + 1 < args.size()) {
            fps = std::stoi(args[++i]);
        } else if (args[i] == "--audio") {
            captureAudio = true;
        }
    }

    // Validate parameters
    if (width <= 0 || width > 4096) {
        std::cerr << "SnackaCaptureWindows: Invalid width (must be 1-4096)\n";
        return 1;
    }
    if (height <= 0 || height > 4096) {
        std::cerr << "SnackaCaptureWindows: Invalid height (must be 1-4096)\n";
        return 1;
    }
    if (fps <= 0 || fps > 120) {
        std::cerr << "SnackaCaptureWindows: Invalid fps (must be 1-120)\n";
        return 1;
    }

    return Capture(displayIndex, windowHandle, width, height, fps, captureAudio);
}
