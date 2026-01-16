#include "CApi.h"
#include "MediaFoundationDecoder.h"
#include <unordered_map>
#include <mutex>

// Instance management
static std::unordered_map<MFDecoderHandle, MediaFoundationDecoder*> s_instances;
static std::mutex s_mutex;

extern "C" {

SNACKA_API MFDecoderHandle mf_decoder_create() {
    try {
        auto* decoder = new MediaFoundationDecoder();

        std::lock_guard<std::mutex> lock(s_mutex);
        s_instances[decoder] = decoder;

        return decoder;
    } catch (...) {
        return nullptr;
    }
}

SNACKA_API void mf_decoder_destroy(MFDecoderHandle handle) {
    if (!handle) return;

    MediaFoundationDecoder* decoder = nullptr;
    {
        std::lock_guard<std::mutex> lock(s_mutex);
        auto it = s_instances.find(handle);
        if (it != s_instances.end()) {
            decoder = it->second;
            s_instances.erase(it);
        }
    }

    delete decoder;
}

SNACKA_API bool mf_decoder_initialize(
    MFDecoderHandle handle,
    int width,
    int height,
    const uint8_t* spsData,
    int spsLength,
    const uint8_t* ppsData,
    int ppsLength
) {
    if (!handle) return false;

    std::lock_guard<std::mutex> lock(s_mutex);
    auto it = s_instances.find(handle);
    if (it == s_instances.end()) return false;

    return it->second->Initialize(width, height, spsData, spsLength, ppsData, ppsLength);
}

SNACKA_API bool mf_decoder_decode_and_render(
    MFDecoderHandle handle,
    const uint8_t* nalData,
    int nalLength,
    bool isKeyframe
) {
    if (!handle) return false;

    std::lock_guard<std::mutex> lock(s_mutex);
    auto it = s_instances.find(handle);
    if (it == s_instances.end()) return false;

    return it->second->DecodeAndRender(nalData, nalLength, isKeyframe);
}

SNACKA_API void* mf_decoder_get_view(MFDecoderHandle handle) {
    if (!handle) return nullptr;

    std::lock_guard<std::mutex> lock(s_mutex);
    auto it = s_instances.find(handle);
    if (it == s_instances.end()) return nullptr;

    return it->second->GetView();
}

SNACKA_API void mf_decoder_set_display_size(
    MFDecoderHandle handle,
    int width,
    int height
) {
    if (!handle) return;

    std::lock_guard<std::mutex> lock(s_mutex);
    auto it = s_instances.find(handle);
    if (it == s_instances.end()) return;

    it->second->SetDisplaySize(width, height);
}

SNACKA_API bool mf_decoder_is_available() {
    return MediaFoundationDecoder::IsAvailable();
}

} // extern "C"
