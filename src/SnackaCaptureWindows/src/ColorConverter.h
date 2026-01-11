#pragma once

#include <d3d11.h>
#include <d3d11_1.h>
#include <wrl/client.h>
#include <cstdint>
#include <vector>

namespace snacka {

using Microsoft::WRL::ComPtr;

// GPU-accelerated BGRA to NV12 converter using D3D11 Video Processor
// Uses dedicated video processing hardware for optimal performance
class GpuColorConverter {
public:
    GpuColorConverter();
    ~GpuColorConverter();

    // Initialize with D3D11 device
    bool Initialize(ID3D11Device* device, int width, int height);

    // Convert BGRA texture to NV12
    // Returns pointer to CPU-accessible NV12 data (valid until next Convert call)
    const uint8_t* Convert(ID3D11DeviceContext* context, ID3D11Texture2D* bgraTexture);

    // Get output size
    size_t GetNV12Size() const { return m_nv12Size; }

    // Get dimensions
    int GetWidth() const { return m_width; }
    int GetHeight() const { return m_height; }

private:
    bool CreateVideoProcessor(ID3D11Device* device);
    bool CreateOutputTextures(ID3D11Device* device);

    int m_width = 0;
    int m_height = 0;
    size_t m_nv12Size = 0;

    // Video processor
    ComPtr<ID3D11VideoDevice> m_videoDevice;
    ComPtr<ID3D11VideoContext> m_videoContext;
    ComPtr<ID3D11VideoProcessorEnumerator> m_videoProcessorEnum;
    ComPtr<ID3D11VideoProcessor> m_videoProcessor;

    // Output NV12 texture (GPU)
    ComPtr<ID3D11Texture2D> m_nv12Texture;
    ComPtr<ID3D11VideoProcessorOutputView> m_outputView;

    // Staging texture (CPU-readable)
    ComPtr<ID3D11Texture2D> m_stagingTexture;

    // CPU buffer for final output
    std::vector<uint8_t> m_nv12Buffer;
};

}  // namespace snacka
