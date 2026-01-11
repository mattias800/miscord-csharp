#include "WindowCapturer.h"
#include <iostream>

// Link against WindowsApp.lib for WinRT
#pragma comment(lib, "windowsapp.lib")

namespace snacka {

// Helper to convert D3D11 device to WinRT IDirect3DDevice
winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice CreateWinRTDevice(ID3D11Device* d3dDevice) {
    ComPtr<IDXGIDevice> dxgiDevice;
    d3dDevice->QueryInterface(IID_PPV_ARGS(&dxgiDevice));

    winrt::com_ptr<::IInspectable> inspectable;
    CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.Get(), inspectable.put());

    return inspectable.as<winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice>();
}

// Helper to get D3D11 texture from WinRT surface
ComPtr<ID3D11Texture2D> GetD3D11Texture(
    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface const& surface) {

    auto access = surface.as<Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
    ComPtr<ID3D11Texture2D> texture;
    access->GetInterface(IID_PPV_ARGS(&texture));
    return texture;
}

WindowCapturer::WindowCapturer() {
    QueryPerformanceFrequency(&m_frequency);
    winrt::init_apartment(winrt::apartment_type::multi_threaded);
}

WindowCapturer::~WindowCapturer() {
    Stop();
}

bool WindowCapturer::IsSupported() {
    return winrt::Windows::Graphics::Capture::GraphicsCaptureSession::IsSupported();
}

bool WindowCapturer::Initialize(HWND hwnd, int width, int height, int fps) {
    if (!IsSupported()) {
        std::cerr << "SnackaCaptureWindows: Windows.Graphics.Capture not supported\n";
        return false;
    }

    m_hwnd = hwnd;
    m_width = width;
    m_height = height;
    m_fps = fps;

    HRESULT hr;

    // Create D3D11 device
    D3D_FEATURE_LEVEL featureLevel;
    UINT createFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

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

    // Create WinRT device wrapper
    m_winrtDevice = CreateWinRTDevice(m_device.Get());

    // Create capture item from window handle
    auto interopFactory = winrt::get_activation_factory<
        winrt::Windows::Graphics::Capture::GraphicsCaptureItem,
        IGraphicsCaptureItemInterop>();

    try {
        interopFactory->CreateForWindow(
            hwnd,
            winrt::guid_of<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem>(),
            winrt::put_abi(m_captureItem));
    } catch (winrt::hresult_error const& ex) {
        std::cerr << "SnackaCaptureWindows: Failed to create capture item: "
                  << winrt::to_string(ex.message()) << "\n";
        return false;
    }

    if (!m_captureItem) {
        std::cerr << "SnackaCaptureWindows: Failed to create capture item for window\n";
        return false;
    }

    // Get window size
    auto itemSize = m_captureItem.Size();
    int windowWidth = itemSize.Width;
    int windowHeight = itemSize.Height;

    std::cerr << "SnackaCaptureWindows: Window size: " << windowWidth << "x" << windowHeight << "\n";

    // Check if we need scaling
    if (width != windowWidth || height != windowHeight) {
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

    // Create frame pool
    m_framePool = winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool::CreateFreeThreaded(
        m_winrtDevice,
        winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,
        2,  // Number of frames to buffer
        itemSize
    );

    // Subscribe to frame arrived event
    m_frameArrivedToken = m_framePool.FrameArrived(
        [this](auto&& sender, auto&& args) { OnFrameArrived(sender, args); });

    // Create capture session
    m_session = m_framePool.CreateCaptureSession(m_captureItem);

    // Configure session (Windows 10 2004+)
    try {
        m_session.IsCursorCaptureEnabled(true);
        m_session.IsBorderRequired(false);
    } catch (...) {
        // These APIs may not be available on older Windows versions
    }

    // Initialize color converter
    m_colorConverter = std::make_unique<GpuColorConverter>();
    if (!m_colorConverter->Initialize(m_device.Get(), width, height)) {
        std::cerr << "SnackaCaptureWindows: Failed to initialize color converter\n";
        return false;
    }

    std::cerr << "SnackaCaptureWindows: Window capture initialized\n";
    return true;
}

void WindowCapturer::Start(FrameCallback callback) {
    if (m_running) return;

    m_callback = callback;
    m_running = true;
    m_session.StartCapture();
}

void WindowCapturer::Stop() {
    if (!m_running) return;

    m_running = false;

    if (m_session) {
        m_session.Close();
        m_session = nullptr;
    }

    if (m_framePool) {
        m_framePool.FrameArrived(m_frameArrivedToken);
        m_framePool.Close();
        m_framePool = nullptr;
    }

    m_captureItem = nullptr;
}

void WindowCapturer::OnFrameArrived(
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool const& sender,
    winrt::Windows::Foundation::IInspectable const&) {

    if (!m_running) return;

    auto frame = sender.TryGetNextFrame();
    if (!frame) return;

    auto surface = frame.Surface();
    auto texture = GetD3D11Texture(surface);

    if (!texture) {
        return;
    }

    // Get timestamp
    LARGE_INTEGER now;
    QueryPerformanceCounter(&now);
    uint64_t timestamp = static_cast<uint64_t>(now.QuadPart * 1000 / m_frequency.QuadPart);

    ComPtr<ID3D11Texture2D> processTexture;

    if (m_needsScaling) {
        // Copy with basic scaling (top-left crop for now)
        // TODO: proper scaling with video processor
        D3D11_BOX srcBox = { 0, 0, 0, static_cast<UINT>(m_width), static_cast<UINT>(m_height), 1 };
        m_context->CopySubresourceRegion(m_scaledTexture.Get(), 0, 0, 0, 0,
                                          texture.Get(), 0, &srcBox);
        processTexture = m_scaledTexture;
    } else {
        // Create a copy for processing
        D3D11_TEXTURE2D_DESC desc;
        texture->GetDesc(&desc);
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        desc.MiscFlags = 0;

        m_device->CreateTexture2D(&desc, nullptr, &processTexture);
        m_context->CopyResource(processTexture.Get(), texture.Get());
    }

    // Convert to NV12
    const uint8_t* nv12Data = m_colorConverter->Convert(m_context.Get(), processTexture.Get());
    if (nv12Data && m_callback) {
        m_callback(nv12Data, m_colorConverter->GetNV12Size(), timestamp);
    }
}

}  // namespace snacka
