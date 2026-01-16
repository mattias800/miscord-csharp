#include "vaapi_decoder.h"
#include "egl_renderer.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>

// Number of surfaces in the pool (H.264 max DPB size + extra)
#define NUM_SURFACES 17

VaapiDecoder* vaapi_decoder_create(void) {
    VaapiDecoder* decoder = (VaapiDecoder*)calloc(1, sizeof(VaapiDecoder));
    if (!decoder) {
        return NULL;
    }

    decoder->drm_fd = -1;
    return decoder;
}

void vaapi_decoder_destroy(VaapiDecoder* decoder) {
    if (!decoder) return;

    // Destroy renderer
    if (decoder->renderer) {
        egl_renderer_destroy(decoder->renderer);
        decoder->renderer = NULL;
    }

    // Destroy VA-API resources
    if (decoder->va_initialized) {
        if (decoder->va_context != VA_INVALID_ID) {
            vaDestroyContext(decoder->va_display, decoder->va_context);
        }
        if (decoder->va_surfaces) {
            vaDestroySurfaces(decoder->va_display, decoder->va_surfaces, decoder->num_surfaces);
            free(decoder->va_surfaces);
        }
        if (decoder->va_config != VA_INVALID_ID) {
            vaDestroyConfig(decoder->va_display, decoder->va_config);
        }
        vaTerminate(decoder->va_display);
    }

    // Close X11 display
    if (decoder->x_display) {
        XCloseDisplay(decoder->x_display);
    }

    // Close DRM fd
    if (decoder->drm_fd >= 0) {
        close(decoder->drm_fd);
    }

    // Free SPS/PPS
    free(decoder->sps);
    free(decoder->pps);

    free(decoder);
}

bool vaapi_decoder_is_available(void) {
    // Try to open X11 display
    Display* display = XOpenDisplay(NULL);
    if (!display) {
        fprintf(stderr, "VaapiDecoder: Cannot open X11 display\n");
        return false;
    }

    // Try X11 VA display first
    VADisplay va_display = vaGetDisplay(display);
    if (va_display == NULL) {
        // Try DRM backend as fallback
        XCloseDisplay(display);

        int drm_fd = open("/dev/dri/renderD128", O_RDWR);
        if (drm_fd < 0) {
            fprintf(stderr, "VaapiDecoder: Cannot open DRM device\n");
            return false;
        }

        va_display = vaGetDisplayDRM(drm_fd);
        if (va_display == NULL) {
            close(drm_fd);
            return false;
        }

        int major, minor;
        VAStatus status = vaInitialize(va_display, &major, &minor);
        if (status != VA_STATUS_SUCCESS) {
            vaTerminate(va_display);
            close(drm_fd);
            return false;
        }

        // Check for H.264 support
        int num_profiles = vaMaxNumProfiles(va_display);
        VAProfile* profiles = (VAProfile*)malloc(num_profiles * sizeof(VAProfile));
        if (!profiles) {
            vaTerminate(va_display);
            close(drm_fd);
            return false;
        }

        status = vaQueryConfigProfiles(va_display, profiles, &num_profiles);
        bool has_h264 = false;

        if (status == VA_STATUS_SUCCESS) {
            for (int i = 0; i < num_profiles; i++) {
                if (profiles[i] == VAProfileH264Main ||
                    profiles[i] == VAProfileH264High ||
                    profiles[i] == VAProfileH264ConstrainedBaseline) {
                    has_h264 = true;
                    break;
                }
            }
        }

        free(profiles);
        vaTerminate(va_display);
        close(drm_fd);

        return has_h264;
    }

    int major, minor;
    VAStatus status = vaInitialize(va_display, &major, &minor);
    if (status != VA_STATUS_SUCCESS) {
        XCloseDisplay(display);
        return false;
    }

    // Check for H.264 support
    int num_profiles = vaMaxNumProfiles(va_display);
    VAProfile* profiles = (VAProfile*)malloc(num_profiles * sizeof(VAProfile));
    if (!profiles) {
        vaTerminate(va_display);
        XCloseDisplay(display);
        return false;
    }

    status = vaQueryConfigProfiles(va_display, profiles, &num_profiles);
    bool has_h264 = false;

    if (status == VA_STATUS_SUCCESS) {
        for (int i = 0; i < num_profiles; i++) {
            if (profiles[i] == VAProfileH264Main ||
                profiles[i] == VAProfileH264High ||
                profiles[i] == VAProfileH264ConstrainedBaseline) {
                has_h264 = true;
                break;
            }
        }
    }

    free(profiles);
    vaTerminate(va_display);
    XCloseDisplay(display);

    return has_h264;
}

static bool init_va_display(VaapiDecoder* decoder) {
    // Open X11 display
    decoder->x_display = XOpenDisplay(NULL);
    if (!decoder->x_display) {
        fprintf(stderr, "VaapiDecoder: Cannot open X11 display\n");
        return false;
    }

    // Try X11 VA display
    decoder->va_display = vaGetDisplay(decoder->x_display);
    if (decoder->va_display == NULL) {
        // Try DRM backend
        decoder->drm_fd = open("/dev/dri/renderD128", O_RDWR);
        if (decoder->drm_fd < 0) {
            fprintf(stderr, "VaapiDecoder: Cannot open DRM device\n");
            return false;
        }

        decoder->va_display = vaGetDisplayDRM(decoder->drm_fd);
        if (decoder->va_display == NULL) {
            fprintf(stderr, "VaapiDecoder: Cannot get VA display\n");
            return false;
        }
    }

    int major, minor;
    VAStatus status = vaInitialize(decoder->va_display, &major, &minor);
    if (status != VA_STATUS_SUCCESS) {
        fprintf(stderr, "VaapiDecoder: vaInitialize failed: %d\n", status);
        return false;
    }

    decoder->va_initialized = true;
    printf("VaapiDecoder: VA-API version %d.%d\n", major, minor);
    return true;
}

static bool create_decoder_context(VaapiDecoder* decoder) {
    // Find H.264 profile
    VAProfile profile = VAProfileH264High;
    VAConfigAttrib attrib;
    attrib.type = VAConfigAttribRTFormat;

    VAStatus status = vaGetConfigAttributes(
        decoder->va_display,
        profile,
        VAEntrypointVLD,
        &attrib, 1
    );

    if (status != VA_STATUS_SUCCESS) {
        // Try other profiles
        profile = VAProfileH264Main;
        status = vaGetConfigAttributes(
            decoder->va_display,
            profile,
            VAEntrypointVLD,
            &attrib, 1
        );
    }

    if (status != VA_STATUS_SUCCESS) {
        fprintf(stderr, "VaapiDecoder: vaGetConfigAttributes failed\n");
        return false;
    }

    // Check for NV12 support
    if (!(attrib.value & VA_RT_FORMAT_YUV420)) {
        fprintf(stderr, "VaapiDecoder: YUV420 format not supported\n");
        return false;
    }

    // Create config
    status = vaCreateConfig(
        decoder->va_display,
        profile,
        VAEntrypointVLD,
        &attrib, 1,
        &decoder->va_config
    );

    if (status != VA_STATUS_SUCCESS) {
        fprintf(stderr, "VaapiDecoder: vaCreateConfig failed: %d\n", status);
        return false;
    }

    // Create surfaces
    decoder->num_surfaces = NUM_SURFACES;
    decoder->va_surfaces = (VASurfaceID*)malloc(decoder->num_surfaces * sizeof(VASurfaceID));
    if (!decoder->va_surfaces) {
        return false;
    }

    status = vaCreateSurfaces(
        decoder->va_display,
        VA_RT_FORMAT_YUV420,
        decoder->width, decoder->height,
        decoder->va_surfaces,
        decoder->num_surfaces,
        NULL, 0
    );

    if (status != VA_STATUS_SUCCESS) {
        fprintf(stderr, "VaapiDecoder: vaCreateSurfaces failed: %d\n", status);
        free(decoder->va_surfaces);
        decoder->va_surfaces = NULL;
        return false;
    }

    // Create context
    status = vaCreateContext(
        decoder->va_display,
        decoder->va_config,
        decoder->width, decoder->height,
        VA_PROGRESSIVE,
        decoder->va_surfaces,
        decoder->num_surfaces,
        &decoder->va_context
    );

    if (status != VA_STATUS_SUCCESS) {
        fprintf(stderr, "VaapiDecoder: vaCreateContext failed: %d\n", status);
        return false;
    }

    return true;
}

bool vaapi_decoder_initialize(
    VaapiDecoder* decoder,
    int width,
    int height,
    const uint8_t* sps,
    int sps_length,
    const uint8_t* pps,
    int pps_length
) {
    if (!decoder || decoder->initialized) {
        return false;
    }

    decoder->width = width;
    decoder->height = height;

    // Copy SPS/PPS
    decoder->sps = (uint8_t*)malloc(sps_length);
    decoder->pps = (uint8_t*)malloc(pps_length);
    if (!decoder->sps || !decoder->pps) {
        return false;
    }
    memcpy(decoder->sps, sps, sps_length);
    memcpy(decoder->pps, pps, pps_length);
    decoder->sps_length = sps_length;
    decoder->pps_length = pps_length;

    // Initialize VA display
    if (!init_va_display(decoder)) {
        return false;
    }

    // Create decoder context
    if (!create_decoder_context(decoder)) {
        return false;
    }

    // Create EGL renderer
    decoder->renderer = egl_renderer_create(decoder->x_display);
    if (!decoder->renderer) {
        fprintf(stderr, "VaapiDecoder: Failed to create EGL renderer\n");
        return false;
    }

    if (!egl_renderer_initialize(decoder->renderer, width, height)) {
        fprintf(stderr, "VaapiDecoder: Failed to initialize EGL renderer\n");
        return false;
    }

    decoder->current_surface = 0;
    decoder->initialized = true;

    printf("VaapiDecoder: Initialized %dx%d\n", width, height);
    return true;
}

bool vaapi_decoder_decode_and_render(
    VaapiDecoder* decoder,
    const uint8_t* nal_data,
    int nal_length,
    bool is_keyframe
) {
    if (!decoder || !decoder->initialized) {
        return false;
    }

    // Get current surface
    VASurfaceID surface = decoder->va_surfaces[decoder->current_surface];

    // Note: Proper H.264 decoding requires parsing the NAL unit to fill
    // VAPictureParameterBufferH264 and VASliceParameterBufferH264.
    // This is a simplified implementation that relies on the decoder
    // to handle the NAL unit directly.

    VAStatus status = vaBeginPicture(decoder->va_display, decoder->va_context, surface);
    if (status != VA_STATUS_SUCCESS) {
        fprintf(stderr, "VaapiDecoder: vaBeginPicture failed: %d\n", status);
        return false;
    }

    // Create slice data buffer
    VABufferID slice_data_buf;
    status = vaCreateBuffer(
        decoder->va_display,
        decoder->va_context,
        VASliceDataBufferType,
        nal_length,
        1,
        (void*)nal_data,
        &slice_data_buf
    );

    if (status != VA_STATUS_SUCCESS) {
        vaEndPicture(decoder->va_display, decoder->va_context);
        fprintf(stderr, "VaapiDecoder: vaCreateBuffer failed: %d\n", status);
        return false;
    }

    // Render picture
    status = vaRenderPicture(decoder->va_display, decoder->va_context, &slice_data_buf, 1);
    if (status != VA_STATUS_SUCCESS) {
        vaDestroyBuffer(decoder->va_display, slice_data_buf);
        vaEndPicture(decoder->va_display, decoder->va_context);
        fprintf(stderr, "VaapiDecoder: vaRenderPicture failed: %d\n", status);
        return false;
    }

    // End picture
    status = vaEndPicture(decoder->va_display, decoder->va_context);
    vaDestroyBuffer(decoder->va_display, slice_data_buf);

    if (status != VA_STATUS_SUCCESS) {
        fprintf(stderr, "VaapiDecoder: vaEndPicture failed: %d\n", status);
        return false;
    }

    // Sync surface
    status = vaSyncSurface(decoder->va_display, surface);
    if (status != VA_STATUS_SUCCESS) {
        fprintf(stderr, "VaapiDecoder: vaSyncSurface failed: %d\n", status);
        return false;
    }

    // Render to display
    if (decoder->renderer) {
        egl_renderer_render_surface(decoder->renderer, decoder->va_display, surface);
    }

    // Advance surface index
    decoder->current_surface = (decoder->current_surface + 1) % decoder->num_surfaces;

    (void)is_keyframe;  // Currently unused
    return true;
}

void* vaapi_decoder_get_view(VaapiDecoder* decoder) {
    if (!decoder || !decoder->renderer) {
        return NULL;
    }

    Window window = egl_renderer_get_window(decoder->renderer);
    return (void*)(uintptr_t)window;
}

void vaapi_decoder_set_display_size(VaapiDecoder* decoder, int width, int height) {
    if (!decoder || !decoder->renderer) {
        return;
    }

    egl_renderer_set_display_size(decoder->renderer, width, height);
}
