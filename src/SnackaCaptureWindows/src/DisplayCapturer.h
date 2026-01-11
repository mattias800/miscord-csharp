#pragma once

#include "Protocol.h"
#include "ColorConverter.h"

#include <d3d11.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <atomic>
#include <functional>
#include <memory>

namespace snacka {

using Microsoft::WRL::ComPtr;

// Callback for frame data
using FrameCallback = std::function<void(const uint8_t* nv12Data, size_t size, uint64_t timestamp)>;

// High-performance display capture using Desktop Duplication API
class DisplayCapturer {
public:
    DisplayCapturer();
    ~DisplayCapturer();

    // Initialize for a specific display
    bool Initialize(int displayIndex, int width, int height, int fps);

    // Start capturing - calls callback for each frame
    void Start(FrameCallback callback);

    // Stop capturing
    void Stop();

    // Check if currently capturing
    bool IsRunning() const { return m_running; }

    // Get actual capture dimensions
    int GetWidth() const { return m_width; }
    int GetHeight() const { return m_height; }

private:
    void CaptureLoop();
    bool AcquireNextFrame(ComPtr<ID3D11Texture2D>& outTexture);
    bool ReinitializeDuplication();

    int m_displayIndex = 0;
    int m_width = 0;
    int m_height = 0;
    int m_fps = 30;
    std::atomic<bool> m_running{false};

    // D3D11 resources
    ComPtr<ID3D11Device> m_device;
    ComPtr<ID3D11DeviceContext> m_context;
    ComPtr<IDXGIOutputDuplication> m_duplication;
    ComPtr<IDXGIOutput1> m_output;

    // For scaling if needed
    ComPtr<ID3D11Texture2D> m_scaledTexture;
    bool m_needsScaling = false;

    // Color converter
    std::unique_ptr<GpuColorConverter> m_colorConverter;

    // Frame callback
    FrameCallback m_callback;

    // Timing
    LARGE_INTEGER m_frequency;
    LARGE_INTEGER m_lastFrameTime;
};

}  // namespace snacka
