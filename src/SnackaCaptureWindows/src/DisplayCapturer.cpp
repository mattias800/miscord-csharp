#include "DisplayCapturer.h"
#include <iostream>
#include <thread>
#include <chrono>

namespace snacka {

DisplayCapturer::DisplayCapturer() {
    QueryPerformanceFrequency(&m_frequency);
}

DisplayCapturer::~DisplayCapturer() {
    Stop();
}

bool DisplayCapturer::Initialize(int displayIndex, int width, int height, int fps) {
    m_displayIndex = displayIndex;
    m_width = width;
    m_height = height;
    m_fps = fps;

    HRESULT hr;

    // Create D3D11 device
    D3D_FEATURE_LEVEL featureLevel;
    UINT createFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#ifdef _DEBUG
    createFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

    hr = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        createFlags,
        nullptr, 0,
        D3D11_SDK_VERSION,
        &m_device,
        &featureLevel,
        &m_context
    );

    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to create D3D11 device\n";
        return false;
    }

    // Get DXGI device
    ComPtr<IDXGIDevice> dxgiDevice;
    hr = m_device->QueryInterface(IID_PPV_ARGS(&dxgiDevice));
    if (FAILED(hr)) return false;

    // Get adapter
    ComPtr<IDXGIAdapter> adapter;
    hr = dxgiDevice->GetAdapter(&adapter);
    if (FAILED(hr)) return false;

    // Enumerate outputs to find the requested display
    ComPtr<IDXGIOutput> output;
    int outputIndex = 0;
    while (adapter->EnumOutputs(outputIndex, &output) != DXGI_ERROR_NOT_FOUND) {
        if (outputIndex == displayIndex) {
            break;
        }
        output.Reset();
        outputIndex++;
    }

    if (!output) {
        std::cerr << "SnackaCaptureWindows: Display " << displayIndex << " not found\n";
        return false;
    }

    // Get output1 interface for duplication
    hr = output->QueryInterface(IID_PPV_ARGS(&m_output));
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to get IDXGIOutput1\n";
        return false;
    }

    // Get output description for actual dimensions
    DXGI_OUTPUT_DESC outputDesc;
    m_output->GetDesc(&outputDesc);
    int nativeWidth = outputDesc.DesktopCoordinates.right - outputDesc.DesktopCoordinates.left;
    int nativeHeight = outputDesc.DesktopCoordinates.bottom - outputDesc.DesktopCoordinates.top;

    std::cerr << "SnackaCaptureWindows: Display " << displayIndex
              << " native resolution: " << nativeWidth << "x" << nativeHeight << "\n";

    // Check if we need scaling
    if (width != nativeWidth || height != nativeHeight) {
        m_needsScaling = true;
        std::cerr << "SnackaCaptureWindows: Will scale to " << width << "x" << height << "\n";

        // Create texture for scaled output
        D3D11_TEXTURE2D_DESC scaledDesc = {};
        scaledDesc.Width = width;
        scaledDesc.Height = height;
        scaledDesc.MipLevels = 1;
        scaledDesc.ArraySize = 1;
        scaledDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        scaledDesc.SampleDesc.Count = 1;
        scaledDesc.Usage = D3D11_USAGE_DEFAULT;
        scaledDesc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;

        hr = m_device->CreateTexture2D(&scaledDesc, nullptr, &m_scaledTexture);
        if (FAILED(hr)) {
            std::cerr << "SnackaCaptureWindows: Failed to create scaled texture\n";
            return false;
        }
    }

    // Create output duplication
    if (!ReinitializeDuplication()) {
        return false;
    }

    // Initialize color converter
    m_colorConverter = std::make_unique<GpuColorConverter>();
    if (!m_colorConverter->Initialize(m_device.Get(), width, height)) {
        std::cerr << "SnackaCaptureWindows: Failed to initialize color converter\n";
        return false;
    }

    std::cerr << "SnackaCaptureWindows: Display capture initialized at "
              << width << "x" << height << " @ " << fps << "fps\n";

    return true;
}

bool DisplayCapturer::ReinitializeDuplication() {
    m_duplication.Reset();

    HRESULT hr = m_output->DuplicateOutput(m_device.Get(), &m_duplication);
    if (FAILED(hr)) {
        if (hr == DXGI_ERROR_NOT_CURRENTLY_AVAILABLE) {
            std::cerr << "SnackaCaptureWindows: Desktop duplication not available "
                      << "(too many apps using it or running over RDP)\n";
        } else if (hr == E_ACCESSDENIED) {
            std::cerr << "SnackaCaptureWindows: Access denied to desktop duplication\n";
        } else {
            std::cerr << "SnackaCaptureWindows: DuplicateOutput failed: 0x"
                      << std::hex << hr << std::dec << "\n";
        }
        return false;
    }

    return true;
}

void DisplayCapturer::Start(FrameCallback callback) {
    if (m_running) return;

    m_callback = callback;
    m_running = true;

    std::thread([this]() { CaptureLoop(); }).detach();
}

void DisplayCapturer::Stop() {
    m_running = false;
}

void DisplayCapturer::CaptureLoop() {
    const auto frameDuration = std::chrono::microseconds(1000000 / m_fps);
    auto nextFrameTime = std::chrono::steady_clock::now();

    while (m_running) {
        auto frameStart = std::chrono::steady_clock::now();

        ComPtr<ID3D11Texture2D> frameTexture;
        if (AcquireNextFrame(frameTexture)) {
            // Get timestamp
            LARGE_INTEGER now;
            QueryPerformanceCounter(&now);
            uint64_t timestamp = static_cast<uint64_t>(now.QuadPart * 1000 / m_frequency.QuadPart);

            // Convert to NV12
            const uint8_t* nv12Data = m_colorConverter->Convert(m_context.Get(), frameTexture.Get());
            if (nv12Data && m_callback) {
                m_callback(nv12Data, m_colorConverter->GetNV12Size(), timestamp);
            }
        }

        // Frame rate limiting
        nextFrameTime += frameDuration;
        auto sleepTime = nextFrameTime - std::chrono::steady_clock::now();
        if (sleepTime > std::chrono::microseconds(0)) {
            std::this_thread::sleep_for(sleepTime);
        } else {
            // We're behind, reset timing
            nextFrameTime = std::chrono::steady_clock::now();
        }
    }
}

bool DisplayCapturer::AcquireNextFrame(ComPtr<ID3D11Texture2D>& outTexture) {
    if (!m_duplication) {
        if (!ReinitializeDuplication()) {
            return false;
        }
    }

    DXGI_OUTDUPL_FRAME_INFO frameInfo;
    ComPtr<IDXGIResource> desktopResource;

    HRESULT hr = m_duplication->AcquireNextFrame(100, &frameInfo, &desktopResource);

    if (hr == DXGI_ERROR_WAIT_TIMEOUT) {
        // No new frame available, use last frame if we have one
        return false;
    }

    if (hr == DXGI_ERROR_ACCESS_LOST) {
        // Need to reinitialize
        std::cerr << "SnackaCaptureWindows: Desktop duplication access lost, reinitializing...\n";
        m_duplication.Reset();
        return false;
    }

    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: AcquireNextFrame failed: 0x"
                  << std::hex << hr << std::dec << "\n";
        return false;
    }

    // Get the desktop texture
    ComPtr<ID3D11Texture2D> desktopTexture;
    hr = desktopResource->QueryInterface(IID_PPV_ARGS(&desktopTexture));
    if (FAILED(hr)) {
        m_duplication->ReleaseFrame();
        return false;
    }

    if (m_needsScaling) {
        // Copy with scaling using the video processor or a simple blit
        // For now, use a simple copy (TODO: proper scaling)
        D3D11_BOX srcBox = { 0, 0, 0, static_cast<UINT>(m_width), static_cast<UINT>(m_height), 1 };
        m_context->CopySubresourceRegion(m_scaledTexture.Get(), 0, 0, 0, 0,
                                          desktopTexture.Get(), 0, &srcBox);
        outTexture = m_scaledTexture;
    } else {
        // Direct copy to a new texture for processing
        // (The desktop texture can't be used directly for some operations)
        D3D11_TEXTURE2D_DESC desc;
        desktopTexture->GetDesc(&desc);
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        desc.MiscFlags = 0;

        ComPtr<ID3D11Texture2D> copyTexture;
        hr = m_device->CreateTexture2D(&desc, nullptr, &copyTexture);
        if (FAILED(hr)) {
            m_duplication->ReleaseFrame();
            return false;
        }

        m_context->CopyResource(copyTexture.Get(), desktopTexture.Get());
        outTexture = copyTexture;
    }

    m_duplication->ReleaseFrame();
    return true;
}

}  // namespace snacka
