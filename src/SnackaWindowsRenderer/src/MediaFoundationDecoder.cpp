#include "MediaFoundationDecoder.h"
#include "D3D11Renderer.h"
#include <mfapi.h>
#include <mfidl.h>
#include <mftransform.h>
#include <mferror.h>
#include <codecapi.h>
#include <iostream>

#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mfuuid.lib")
#pragma comment(lib, "mf.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")

MediaFoundationDecoder::MediaFoundationDecoder() {
}

MediaFoundationDecoder::~MediaFoundationDecoder() {
    Cleanup();
}

void MediaFoundationDecoder::Cleanup() {
    if (m_decoder) {
        m_decoder->ProcessMessage(MFT_MESSAGE_NOTIFY_END_STREAMING, 0);
        m_decoder->Release();
        m_decoder = nullptr;
    }

    if (m_deviceManager) {
        m_deviceManager->Release();
        m_deviceManager = nullptr;
    }

    if (m_context) {
        m_context->Release();
        m_context = nullptr;
    }

    if (m_device) {
        m_device->Release();
        m_device = nullptr;
    }

    if (m_mfInitialized) {
        MFShutdown();
        m_mfInitialized = false;
    }

    m_renderer.reset();
    m_initialized = false;
}

bool MediaFoundationDecoder::IsAvailable() {
    // Initialize MF temporarily to check availability
    HRESULT hr = MFStartup(MF_VERSION);
    if (FAILED(hr)) {
        return false;
    }

    // Check if H.264 decoder is available
    MFT_REGISTER_TYPE_INFO inputType = { MFMediaType_Video, MFVideoFormat_H264 };
    MFT_REGISTER_TYPE_INFO outputType = { MFMediaType_Video, MFVideoFormat_NV12 };

    IMFActivate** ppActivate = nullptr;
    UINT32 count = 0;

    hr = MFTEnumEx(
        MFT_CATEGORY_VIDEO_DECODER,
        MFT_ENUM_FLAG_HARDWARE | MFT_ENUM_FLAG_SORTANDFILTER,
        &inputType,
        &outputType,
        &ppActivate,
        &count
    );

    if (FAILED(hr) || count == 0) {
        // Try software decoder
        hr = MFTEnumEx(
            MFT_CATEGORY_VIDEO_DECODER,
            MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_SORTANDFILTER,
            &inputType,
            &outputType,
            &ppActivate,
            &count
        );
    }

    // Cleanup
    for (UINT32 i = 0; i < count; i++) {
        ppActivate[i]->Release();
    }
    if (ppActivate) {
        CoTaskMemFree(ppActivate);
    }

    MFShutdown();

    return count > 0;
}

bool MediaFoundationDecoder::Initialize(int width, int height,
                                         const uint8_t* sps, int spsLen,
                                         const uint8_t* pps, int ppsLen) {
    if (m_initialized) {
        return false;
    }

    m_width = width;
    m_height = height;
    m_sps.assign(sps, sps + spsLen);
    m_pps.assign(pps, pps + ppsLen);

    // Initialize Media Foundation
    HRESULT hr = MFStartup(MF_VERSION);
    if (FAILED(hr)) {
        std::cerr << "MediaFoundationDecoder: MFStartup failed: " << std::hex << hr << std::endl;
        return false;
    }
    m_mfInitialized = true;

    // Create D3D11 device
    if (!CreateD3D11Device()) {
        std::cerr << "MediaFoundationDecoder: Failed to create D3D11 device" << std::endl;
        Cleanup();
        return false;
    }

    // Create decoder
    if (!CreateDecoder()) {
        std::cerr << "MediaFoundationDecoder: Failed to create decoder" << std::endl;
        Cleanup();
        return false;
    }

    // Configure decoder
    if (!ConfigureDecoder()) {
        std::cerr << "MediaFoundationDecoder: Failed to configure decoder" << std::endl;
        Cleanup();
        return false;
    }

    // Create renderer
    m_renderer = std::make_unique<D3D11Renderer>(m_device, m_context);
    if (!m_renderer->Initialize(width, height)) {
        std::cerr << "MediaFoundationDecoder: Failed to initialize renderer" << std::endl;
        Cleanup();
        return false;
    }

    // Start streaming
    hr = m_decoder->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0);
    if (FAILED(hr)) {
        std::cerr << "MediaFoundationDecoder: Failed to start streaming" << std::endl;
        Cleanup();
        return false;
    }

    m_initialized = true;
    std::cout << "MediaFoundationDecoder: Initialized " << width << "x" << height << std::endl;
    return true;
}

bool MediaFoundationDecoder::CreateD3D11Device() {
    D3D_FEATURE_LEVEL featureLevels[] = {
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0,
        D3D_FEATURE_LEVEL_10_1,
        D3D_FEATURE_LEVEL_10_0
    };

    UINT creationFlags = D3D11_CREATE_DEVICE_VIDEO_SUPPORT;
#ifdef _DEBUG
    creationFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

    D3D_FEATURE_LEVEL featureLevel;
    HRESULT hr = D3D11CreateDevice(
        nullptr,                    // Default adapter
        D3D_DRIVER_TYPE_HARDWARE,   // Hardware device
        nullptr,                    // No software rasterizer
        creationFlags,
        featureLevels,
        ARRAYSIZE(featureLevels),
        D3D11_SDK_VERSION,
        &m_device,
        &featureLevel,
        &m_context
    );

    if (FAILED(hr)) {
        std::cerr << "MediaFoundationDecoder: D3D11CreateDevice failed: " << std::hex << hr << std::endl;
        return false;
    }

    // Create DXGI device manager for hardware acceleration
    hr = MFCreateDXGIDeviceManager(&m_resetToken, &m_deviceManager);
    if (FAILED(hr)) {
        std::cerr << "MediaFoundationDecoder: MFCreateDXGIDeviceManager failed" << std::endl;
        return false;
    }

    hr = m_deviceManager->ResetDevice(m_device, m_resetToken);
    if (FAILED(hr)) {
        std::cerr << "MediaFoundationDecoder: ResetDevice failed" << std::endl;
        return false;
    }

    // Enable multi-threaded D3D11 for MF
    ID3D10Multithread* multithread = nullptr;
    hr = m_device->QueryInterface(__uuidof(ID3D10Multithread), (void**)&multithread);
    if (SUCCEEDED(hr) && multithread) {
        multithread->SetMultithreadProtected(TRUE);
        multithread->Release();
    }

    return true;
}

bool MediaFoundationDecoder::CreateDecoder() {
    MFT_REGISTER_TYPE_INFO inputType = { MFMediaType_Video, MFVideoFormat_H264 };
    MFT_REGISTER_TYPE_INFO outputType = { MFMediaType_Video, MFVideoFormat_NV12 };

    IMFActivate** ppActivate = nullptr;
    UINT32 count = 0;

    // Try hardware decoder first
    HRESULT hr = MFTEnumEx(
        MFT_CATEGORY_VIDEO_DECODER,
        MFT_ENUM_FLAG_HARDWARE | MFT_ENUM_FLAG_SORTANDFILTER,
        &inputType,
        &outputType,
        &ppActivate,
        &count
    );

    if (FAILED(hr) || count == 0) {
        // Fall back to software decoder
        std::cout << "MediaFoundationDecoder: No hardware decoder, trying software..." << std::endl;
        hr = MFTEnumEx(
            MFT_CATEGORY_VIDEO_DECODER,
            MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_SORTANDFILTER,
            &inputType,
            &outputType,
            &ppActivate,
            &count
        );
    }

    if (FAILED(hr) || count == 0) {
        std::cerr << "MediaFoundationDecoder: No H.264 decoder available" << std::endl;
        return false;
    }

    // Activate the first decoder
    hr = ppActivate[0]->ActivateObject(IID_PPV_ARGS(&m_decoder));

    // Cleanup activation objects
    for (UINT32 i = 0; i < count; i++) {
        ppActivate[i]->Release();
    }
    CoTaskMemFree(ppActivate);

    if (FAILED(hr)) {
        std::cerr << "MediaFoundationDecoder: Failed to activate decoder" << std::endl;
        return false;
    }

    // Try to use D3D11 device manager
    hr = m_decoder->ProcessMessage(MFT_MESSAGE_SET_D3D_MANAGER, (ULONG_PTR)m_deviceManager);
    if (FAILED(hr)) {
        // Not all decoders support D3D11, continue without it
        std::cout << "MediaFoundationDecoder: Decoder doesn't support D3D11 (using software path)" << std::endl;
    }

    return true;
}

bool MediaFoundationDecoder::ConfigureDecoder() {
    // Set input type (H.264)
    IMFMediaType* inputMediaType = nullptr;
    HRESULT hr = MFCreateMediaType(&inputMediaType);
    if (FAILED(hr)) return false;

    inputMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    inputMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
    inputMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    MFSetAttributeSize(inputMediaType, MF_MT_FRAME_SIZE, m_width, m_height);
    MFSetAttributeRatio(inputMediaType, MF_MT_FRAME_RATE, 30, 1);
    MFSetAttributeRatio(inputMediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    hr = m_decoder->SetInputType(0, inputMediaType, 0);
    inputMediaType->Release();

    if (FAILED(hr)) {
        std::cerr << "MediaFoundationDecoder: SetInputType failed: " << std::hex << hr << std::endl;
        return false;
    }

    // Find and set output type (NV12)
    IMFMediaType* outputMediaType = nullptr;
    for (DWORD i = 0; ; i++) {
        hr = m_decoder->GetOutputAvailableType(0, i, &outputMediaType);
        if (FAILED(hr)) break;

        GUID subtype;
        hr = outputMediaType->GetGUID(MF_MT_SUBTYPE, &subtype);
        if (SUCCEEDED(hr) && subtype == MFVideoFormat_NV12) {
            hr = m_decoder->SetOutputType(0, outputMediaType, 0);
            outputMediaType->Release();
            if (SUCCEEDED(hr)) {
                return true;
            }
        } else {
            outputMediaType->Release();
        }
    }

    // If NV12 not found, create and set manually
    hr = MFCreateMediaType(&outputMediaType);
    if (FAILED(hr)) return false;

    outputMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    outputMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_NV12);
    MFSetAttributeSize(outputMediaType, MF_MT_FRAME_SIZE, m_width, m_height);

    hr = m_decoder->SetOutputType(0, outputMediaType, 0);
    outputMediaType->Release();

    if (FAILED(hr)) {
        std::cerr << "MediaFoundationDecoder: SetOutputType failed: " << std::hex << hr << std::endl;
        return false;
    }

    return true;
}

IMFSample* MediaFoundationDecoder::CreateSampleFromNAL(const uint8_t* nalData, int nalLen, bool isKeyframe) {
    // Create buffer with 4-byte AVCC length prefix
    int totalLength = nalLen + 4;

    IMFMediaBuffer* buffer = nullptr;
    HRESULT hr = MFCreateMemoryBuffer(totalLength, &buffer);
    if (FAILED(hr)) return nullptr;

    BYTE* bufferData = nullptr;
    hr = buffer->Lock(&bufferData, nullptr, nullptr);
    if (FAILED(hr)) {
        buffer->Release();
        return nullptr;
    }

    // Write length prefix (big-endian)
    bufferData[0] = (nalLen >> 24) & 0xFF;
    bufferData[1] = (nalLen >> 16) & 0xFF;
    bufferData[2] = (nalLen >> 8) & 0xFF;
    bufferData[3] = nalLen & 0xFF;

    // Copy NAL data
    memcpy(bufferData + 4, nalData, nalLen);

    buffer->Unlock();
    buffer->SetCurrentLength(totalLength);

    // Create sample
    IMFSample* sample = nullptr;
    hr = MFCreateSample(&sample);
    if (FAILED(hr)) {
        buffer->Release();
        return nullptr;
    }

    sample->AddBuffer(buffer);
    buffer->Release();

    // Set sample attributes
    sample->SetSampleTime(0);
    sample->SetSampleDuration(0);

    if (isKeyframe) {
        sample->SetUINT32(MFSampleExtension_CleanPoint, TRUE);
    }

    return sample;
}

bool MediaFoundationDecoder::DecodeAndRender(const uint8_t* nalData, int nalLen, bool isKeyframe) {
    if (!m_initialized || !m_decoder) {
        return false;
    }

    // Create input sample
    IMFSample* inputSample = CreateSampleFromNAL(nalData, nalLen, isKeyframe);
    if (!inputSample) {
        return false;
    }

    // Feed to decoder
    HRESULT hr = m_decoder->ProcessInput(0, inputSample, 0);
    inputSample->Release();

    if (FAILED(hr) && hr != MF_E_NOTACCEPTING) {
        std::cerr << "MediaFoundationDecoder: ProcessInput failed: " << std::hex << hr << std::endl;
        return false;
    }

    // Try to get output
    MFT_OUTPUT_DATA_BUFFER outputBuffer = {};
    outputBuffer.dwStreamID = 0;
    outputBuffer.pSample = nullptr;
    outputBuffer.dwStatus = 0;
    outputBuffer.pEvents = nullptr;

    // Check if decoder allocates its own samples
    MFT_OUTPUT_STREAM_INFO streamInfo = {};
    m_decoder->GetOutputStreamInfo(0, &streamInfo);

    bool decoderAllocates = (streamInfo.dwFlags &
        (MFT_OUTPUT_STREAM_PROVIDES_SAMPLES | MFT_OUTPUT_STREAM_CAN_PROVIDE_SAMPLES)) != 0;

    IMFSample* outputSample = nullptr;
    if (!decoderAllocates) {
        // We need to provide output sample
        MFCreateSample(&outputSample);
        IMFMediaBuffer* outBuffer = nullptr;
        // Allocate NV12 buffer (width * height * 1.5)
        MFCreateMemoryBuffer(m_width * m_height * 3 / 2, &outBuffer);
        outputSample->AddBuffer(outBuffer);
        outBuffer->Release();
        outputBuffer.pSample = outputSample;
    }

    DWORD status = 0;
    hr = m_decoder->ProcessOutput(0, 1, &outputBuffer, &status);

    if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT) {
        // Decoder needs more data - not an error
        if (outputSample) outputSample->Release();
        return true;
    }

    if (hr == MF_E_TRANSFORM_STREAM_CHANGE) {
        // Output format changed, reconfigure
        if (outputSample) outputSample->Release();
        // Handle format change if needed
        return true;
    }

    if (FAILED(hr)) {
        if (outputSample) outputSample->Release();
        return false;
    }

    // Render the decoded frame
    IMFSample* decodedSample = outputBuffer.pSample;
    if (decodedSample) {
        RenderFrame(decodedSample);
        decodedSample->Release();
    }

    return true;
}

void MediaFoundationDecoder::RenderFrame(IMFSample* sample) {
    if (!m_renderer) return;

    IMFMediaBuffer* buffer = nullptr;
    HRESULT hr = sample->GetBufferByIndex(0, &buffer);
    if (FAILED(hr)) return;

    // Try to get D3D11 texture from buffer
    IMFDXGIBuffer* dxgiBuffer = nullptr;
    hr = buffer->QueryInterface(IID_PPV_ARGS(&dxgiBuffer));

    if (SUCCEEDED(hr) && dxgiBuffer) {
        // Got hardware-decoded texture
        ID3D11Texture2D* texture = nullptr;
        UINT subresource = 0;
        hr = dxgiBuffer->GetResource(IID_PPV_ARGS(&texture));
        if (SUCCEEDED(hr) && texture) {
            dxgiBuffer->GetSubresourceIndex(&subresource);
            m_renderer->RenderNV12Texture(texture);
            texture->Release();
        }
        dxgiBuffer->Release();
    } else {
        // Software decoded - buffer contains raw NV12 data
        // For now, just skip - would need to upload to texture
        std::cerr << "MediaFoundationDecoder: Software decode path not implemented" << std::endl;
    }

    buffer->Release();
}

HWND MediaFoundationDecoder::GetView() const {
    return m_renderer ? m_renderer->GetHwnd() : nullptr;
}

void MediaFoundationDecoder::SetDisplaySize(int width, int height) {
    if (m_renderer) {
        m_renderer->SetDisplaySize(width, height);
    }
}
