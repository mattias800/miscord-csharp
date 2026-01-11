#include "ColorConverter.h"
#include <iostream>

namespace snacka {

GpuColorConverter::GpuColorConverter() = default;

GpuColorConverter::~GpuColorConverter() = default;

bool GpuColorConverter::Initialize(ID3D11Device* device, int width, int height) {
    m_width = width;
    m_height = height;
    m_nv12Size = static_cast<size_t>(width) * height * 3 / 2;
    m_nv12Buffer.resize(m_nv12Size);

    if (!CreateVideoProcessor(device)) {
        std::cerr << "SnackaCaptureWindows: Failed to create video processor\n";
        return false;
    }

    if (!CreateOutputTextures(device)) {
        std::cerr << "SnackaCaptureWindows: Failed to create output textures\n";
        return false;
    }

    std::cerr << "SnackaCaptureWindows: Video processor initialized for "
              << width << "x" << height << " BGRA->NV12 conversion\n";
    return true;
}

bool GpuColorConverter::CreateVideoProcessor(ID3D11Device* device) {
    HRESULT hr;

    // Get video device interface
    hr = device->QueryInterface(IID_PPV_ARGS(&m_videoDevice));
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Device doesn't support video processing\n";
        return false;
    }

    // Get video context
    ComPtr<ID3D11DeviceContext> context;
    device->GetImmediateContext(&context);
    hr = context->QueryInterface(IID_PPV_ARGS(&m_videoContext));
    if (FAILED(hr)) {
        return false;
    }

    // Create video processor enumerator
    D3D11_VIDEO_PROCESSOR_CONTENT_DESC contentDesc = {};
    contentDesc.InputFrameFormat = D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE;
    contentDesc.InputWidth = m_width;
    contentDesc.InputHeight = m_height;
    contentDesc.OutputWidth = m_width;
    contentDesc.OutputHeight = m_height;
    contentDesc.Usage = D3D11_VIDEO_USAGE_PLAYBACK_NORMAL;

    hr = m_videoDevice->CreateVideoProcessorEnumerator(&contentDesc, &m_videoProcessorEnum);
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to create video processor enumerator\n";
        return false;
    }

    // Check if BGRA input is supported
    UINT formatSupport = 0;
    hr = m_videoProcessorEnum->CheckVideoProcessorFormat(DXGI_FORMAT_B8G8R8A8_UNORM, &formatSupport);
    if (FAILED(hr) || !(formatSupport & D3D11_VIDEO_PROCESSOR_FORMAT_SUPPORT_INPUT)) {
        std::cerr << "SnackaCaptureWindows: BGRA input not supported\n";
        return false;
    }

    // Check if NV12 output is supported
    hr = m_videoProcessorEnum->CheckVideoProcessorFormat(DXGI_FORMAT_NV12, &formatSupport);
    if (FAILED(hr) || !(formatSupport & D3D11_VIDEO_PROCESSOR_FORMAT_SUPPORT_OUTPUT)) {
        std::cerr << "SnackaCaptureWindows: NV12 output not supported\n";
        return false;
    }

    // Create video processor
    hr = m_videoDevice->CreateVideoProcessor(m_videoProcessorEnum.Get(), 0, &m_videoProcessor);
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to create video processor\n";
        return false;
    }

    // Configure color space conversion
    D3D11_VIDEO_PROCESSOR_COLOR_SPACE inputColorSpace = {};
    inputColorSpace.Usage = 0;  // 0 = playback, 1 = video processing
    inputColorSpace.RGB_Range = 0;  // 0 = full range (0-255), 1 = studio range (16-235)
    inputColorSpace.YCbCr_Matrix = 1;  // 0 = BT.601, 1 = BT.709
    inputColorSpace.YCbCr_xvYCC = 0;
    inputColorSpace.Nominal_Range = D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_0_255;
    m_videoContext->VideoProcessorSetStreamColorSpace(m_videoProcessor.Get(), 0, &inputColorSpace);

    D3D11_VIDEO_PROCESSOR_COLOR_SPACE outputColorSpace = {};
    outputColorSpace.Usage = 0;
    outputColorSpace.RGB_Range = 1;  // Studio range for NV12 output
    outputColorSpace.YCbCr_Matrix = 1;  // BT.709
    outputColorSpace.YCbCr_xvYCC = 0;
    outputColorSpace.Nominal_Range = D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_16_235;
    m_videoContext->VideoProcessorSetOutputColorSpace(m_videoProcessor.Get(), &outputColorSpace);

    return true;
}

bool GpuColorConverter::CreateOutputTextures(ID3D11Device* device) {
    HRESULT hr;

    // Create NV12 output texture
    D3D11_TEXTURE2D_DESC nv12Desc = {};
    nv12Desc.Width = m_width;
    nv12Desc.Height = m_height;
    nv12Desc.MipLevels = 1;
    nv12Desc.ArraySize = 1;
    nv12Desc.Format = DXGI_FORMAT_NV12;
    nv12Desc.SampleDesc.Count = 1;
    nv12Desc.Usage = D3D11_USAGE_DEFAULT;
    nv12Desc.BindFlags = D3D11_BIND_RENDER_TARGET;

    hr = device->CreateTexture2D(&nv12Desc, nullptr, &m_nv12Texture);
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to create NV12 texture\n";
        return false;
    }

    // Create output view for video processor
    D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC outputViewDesc = {};
    outputViewDesc.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;
    outputViewDesc.Texture2D.MipSlice = 0;

    hr = m_videoDevice->CreateVideoProcessorOutputView(
        m_nv12Texture.Get(), m_videoProcessorEnum.Get(), &outputViewDesc, &m_outputView);
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to create output view\n";
        return false;
    }

    // Create staging texture for CPU readback
    D3D11_TEXTURE2D_DESC stagingDesc = nv12Desc;
    stagingDesc.Usage = D3D11_USAGE_STAGING;
    stagingDesc.BindFlags = 0;
    stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;

    hr = device->CreateTexture2D(&stagingDesc, nullptr, &m_stagingTexture);
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to create staging texture\n";
        return false;
    }

    return true;
}

const uint8_t* GpuColorConverter::Convert(ID3D11DeviceContext* context, ID3D11Texture2D* bgraTexture) {
    HRESULT hr;

    // Create input view for the BGRA texture
    D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC inputViewDesc = {};
    inputViewDesc.FourCC = 0;
    inputViewDesc.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
    inputViewDesc.Texture2D.MipSlice = 0;
    inputViewDesc.Texture2D.ArraySlice = 0;

    ComPtr<ID3D11VideoProcessorInputView> inputView;
    hr = m_videoDevice->CreateVideoProcessorInputView(
        bgraTexture, m_videoProcessorEnum.Get(), &inputViewDesc, &inputView);
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to create input view\n";
        return nullptr;
    }

    // Set up the stream
    D3D11_VIDEO_PROCESSOR_STREAM stream = {};
    stream.Enable = TRUE;
    stream.OutputIndex = 0;
    stream.InputFrameOrField = 0;
    stream.PastFrames = 0;
    stream.FutureFrames = 0;
    stream.pInputSurface = inputView.Get();

    // Run the video processor (BGRA -> NV12)
    hr = m_videoContext->VideoProcessorBlt(
        m_videoProcessor.Get(),
        m_outputView.Get(),
        0,  // Output frame
        1,  // Stream count
        &stream
    );
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: VideoProcessorBlt failed: 0x" << std::hex << hr << std::dec << "\n";
        return nullptr;
    }

    // Copy NV12 result to staging texture
    context->CopyResource(m_stagingTexture.Get(), m_nv12Texture.Get());

    // Map staging texture
    D3D11_MAPPED_SUBRESOURCE mapped;
    hr = context->Map(m_stagingTexture.Get(), 0, D3D11_MAP_READ, 0, &mapped);
    if (FAILED(hr)) {
        std::cerr << "SnackaCaptureWindows: Failed to map staging texture\n";
        return nullptr;
    }

    // NV12 layout: Y plane (full height), then UV plane (half height)
    // Both planes have the same row pitch from Map()
    const uint8_t* src = static_cast<const uint8_t*>(mapped.pData);
    uint8_t* dst = m_nv12Buffer.data();

    // Copy Y plane (full resolution)
    for (int y = 0; y < m_height; y++) {
        memcpy(dst + y * m_width, src + y * mapped.RowPitch, m_width);
    }

    // Copy UV plane (half height, interleaved U and V)
    // UV plane starts at mapped.RowPitch * m_height in the texture
    const uint8_t* uvSrc = src + mapped.RowPitch * m_height;
    uint8_t* uvDst = dst + m_width * m_height;
    int uvHeight = m_height / 2;

    for (int y = 0; y < uvHeight; y++) {
        memcpy(uvDst + y * m_width, uvSrc + y * mapped.RowPitch, m_width);
    }

    context->Unmap(m_stagingTexture.Get(), 0);

    return m_nv12Buffer.data();
}

}  // namespace snacka
