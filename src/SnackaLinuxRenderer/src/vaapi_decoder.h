#ifndef VAAPI_DECODER_H
#define VAAPI_DECODER_H

#include <stdbool.h>
#include <stdint.h>
#include <va/va.h>
#include <va/va_x11.h>
#include <va/va_drm.h>
#include <X11/Xlib.h>

// Forward declarations
struct EglRenderer;

// VA-API decoder structure
typedef struct VaapiDecoder {
    // VA-API
    VADisplay va_display;
    VAConfigID va_config;
    VAContextID va_context;
    VASurfaceID* va_surfaces;
    int num_surfaces;
    int current_surface;

    // Video parameters
    int width;
    int height;
    uint8_t* sps;
    int sps_length;
    uint8_t* pps;
    int pps_length;

    // X11 Display
    Display* x_display;

    // EGL renderer
    struct EglRenderer* renderer;

    // State
    bool initialized;
    bool va_initialized;

    // DRM fd (if using DRM backend)
    int drm_fd;
} VaapiDecoder;

// Create a new decoder
VaapiDecoder* vaapi_decoder_create(void);

// Destroy a decoder
void vaapi_decoder_destroy(VaapiDecoder* decoder);

// Initialize the decoder
bool vaapi_decoder_initialize(
    VaapiDecoder* decoder,
    int width,
    int height,
    const uint8_t* sps,
    int sps_length,
    const uint8_t* pps,
    int pps_length
);

// Decode and render a NAL unit
bool vaapi_decoder_decode_and_render(
    VaapiDecoder* decoder,
    const uint8_t* nal_data,
    int nal_length,
    bool is_keyframe
);

// Get the X11 window handle
void* vaapi_decoder_get_view(VaapiDecoder* decoder);

// Set display size
void vaapi_decoder_set_display_size(VaapiDecoder* decoder, int width, int height);

// Check if VA-API is available
bool vaapi_decoder_is_available(void);

#endif // VAAPI_DECODER_H
