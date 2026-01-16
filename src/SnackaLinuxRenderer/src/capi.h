#ifndef SNACKA_LINUX_RENDERER_H
#define SNACKA_LINUX_RENDERER_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifdef SNACKA_RENDERER_EXPORTS
#define SNACKA_API __attribute__((visibility("default")))
#else
#define SNACKA_API
#endif

// Opaque handle to decoder instance
typedef void* VaDecoderHandle;

// Create a new decoder instance
// Returns: Handle to decoder, or NULL on failure
SNACKA_API VaDecoderHandle va_decoder_create(void);

// Destroy a decoder instance
SNACKA_API void va_decoder_destroy(VaDecoderHandle decoder);

// Initialize decoder with video parameters
// spsData/ppsData: H264 parameter sets (without Annex B start codes)
// Returns: true on success
SNACKA_API bool va_decoder_initialize(
    VaDecoderHandle decoder,
    int width,
    int height,
    const uint8_t* spsData,
    int spsLength,
    const uint8_t* ppsData,
    int ppsLength
);

// Decode an H264 NAL unit and render to the display surface
// nalData: NAL unit bytes (without Annex B start code)
// isKeyframe: true if this is an IDR frame
// Returns: true on successful decode and render
SNACKA_API bool va_decoder_decode_and_render(
    VaDecoderHandle decoder,
    const uint8_t* nalData,
    int nalLength,
    bool isKeyframe
);

// Get the native window handle for embedding
// Returns: X11 Window ID (XID) as pointer-sized integer
SNACKA_API void* va_decoder_get_view(VaDecoderHandle decoder);

// Set the display size (for the renderer window)
SNACKA_API void va_decoder_set_display_size(
    VaDecoderHandle decoder,
    int width,
    int height
);

// Check if VA-API H264 decoding is available
SNACKA_API bool va_decoder_is_available(void);

#ifdef __cplusplus
}
#endif

#endif // SNACKA_LINUX_RENDERER_H
