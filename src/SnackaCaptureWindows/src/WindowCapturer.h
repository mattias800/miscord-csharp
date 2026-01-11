#pragma once

#include "Protocol.h"
#include "ColorConverter.h"

#include <d3d11.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <atomic>
#include <functional>
#include <memory>

// Windows.Graphics.Capture requires WinRT
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>

namespace snacka {

using Microsoft::WRL::ComPtr;
using FrameCallback = std::function<void(const uint8_t* nv12Data, size_t size, uint64_t timestamp)>;

// Window capture using Windows.Graphics.Capture API
// Requires Windows 10 version 1903 or later
class WindowCapturer {
public:
    WindowCapturer();
    ~WindowCapturer();

    // Initialize for a specific window by HWND
    bool Initialize(HWND hwnd, int width, int height, int fps);

    // Start capturing - calls callback for each frame
    void Start(FrameCallback callback);

    // Stop capturing
    void Stop();

    // Check if currently capturing
    bool IsRunning() const { return m_running; }

    // Get actual capture dimensions
    int GetWidth() const { return m_width; }
    int GetHeight() const { return m_height; }

    // Check if Windows.Graphics.Capture is available
    static bool IsSupported();

private:
    void OnFrameArrived(
        winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool const& sender,
        winrt::Windows::Foundation::IInspectable const& args);

    HWND m_hwnd = nullptr;
    int m_width = 0;
    int m_height = 0;
    int m_fps = 30;
    std::atomic<bool> m_running{false};

    // D3D11 resources
    ComPtr<ID3D11Device> m_device;
    ComPtr<ID3D11DeviceContext> m_context;

    // WinRT capture objects
    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice m_winrtDevice{nullptr};
    winrt::Windows::Graphics::Capture::GraphicsCaptureItem m_captureItem{nullptr};
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool m_framePool{nullptr};
    winrt::Windows::Graphics::Capture::GraphicsCaptureSession m_session{nullptr};
    winrt::event_token m_frameArrivedToken;

    // For scaling
    ComPtr<ID3D11Texture2D> m_scaledTexture;
    bool m_needsScaling = false;

    // Color converter
    std::unique_ptr<GpuColorConverter> m_colorConverter;

    // Frame callback
    FrameCallback m_callback;

    // Timing
    LARGE_INTEGER m_frequency;
};

}  // namespace snacka
