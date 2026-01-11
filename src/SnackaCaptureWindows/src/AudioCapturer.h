#pragma once

#include "Protocol.h"

#include <Windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <wrl/client.h>
#include <atomic>
#include <functional>
#include <vector>
#include <thread>

namespace snacka {

using Microsoft::WRL::ComPtr;

// Callback for audio data (already normalized to 48kHz 16-bit stereo)
using AudioCallback = std::function<void(const uint8_t* data, size_t size, uint64_t timestamp)>;

// System audio capture using WASAPI loopback
// Captures all system audio and normalizes to 48kHz 16-bit stereo
class AudioCapturer {
public:
    AudioCapturer();
    ~AudioCapturer();

    // Initialize audio capture
    bool Initialize();

    // Start capturing - calls callback for each audio packet
    void Start(AudioCallback callback);

    // Stop capturing
    void Stop();

    // Check if currently capturing
    bool IsRunning() const { return m_running; }

private:
    void CaptureLoop();

    // Normalize audio from WASAPI format to 48kHz 16-bit stereo
    void NormalizeAudio(const BYTE* inputData, UINT32 numFrames,
                        std::vector<int16_t>& outputBuffer);

    std::atomic<bool> m_running{false};
    std::thread m_captureThread;

    // WASAPI objects
    ComPtr<IMMDeviceEnumerator> m_deviceEnumerator;
    ComPtr<IMMDevice> m_device;
    ComPtr<IAudioClient> m_audioClient;
    ComPtr<IAudioCaptureClient> m_captureClient;

    // Audio format from WASAPI
    WAVEFORMATEX* m_waveFormat = nullptr;
    bool m_isFloat = false;
    int m_bitsPerSample = 0;
    int m_channels = 0;
    int m_sampleRate = 0;

    // Resampling buffer (for non-48kHz sources)
    std::vector<float> m_resampleBuffer;

    // Output buffer (48kHz 16-bit stereo)
    std::vector<int16_t> m_outputBuffer;

    // Callback
    AudioCallback m_callback;

    // Timing
    LARGE_INTEGER m_frequency;
    LARGE_INTEGER m_startTime;
};

}  // namespace snacka
