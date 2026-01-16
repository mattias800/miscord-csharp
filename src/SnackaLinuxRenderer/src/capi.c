#include "capi.h"
#include "vaapi_decoder.h"
#include <stdlib.h>
#include <pthread.h>

// Simple linked list for instance tracking
typedef struct InstanceNode {
    VaDecoderHandle handle;
    VaapiDecoder* decoder;
    struct InstanceNode* next;
} InstanceNode;

static InstanceNode* s_instances = NULL;
static pthread_mutex_t s_mutex = PTHREAD_MUTEX_INITIALIZER;

static VaapiDecoder* find_decoder(VaDecoderHandle handle) {
    InstanceNode* node = s_instances;
    while (node) {
        if (node->handle == handle) {
            return node->decoder;
        }
        node = node->next;
    }
    return NULL;
}

static void add_instance(VaDecoderHandle handle, VaapiDecoder* decoder) {
    InstanceNode* node = (InstanceNode*)malloc(sizeof(InstanceNode));
    if (!node) return;

    node->handle = handle;
    node->decoder = decoder;
    node->next = s_instances;
    s_instances = node;
}

static VaapiDecoder* remove_instance(VaDecoderHandle handle) {
    InstanceNode** pp = &s_instances;
    while (*pp) {
        if ((*pp)->handle == handle) {
            InstanceNode* node = *pp;
            VaapiDecoder* decoder = node->decoder;
            *pp = node->next;
            free(node);
            return decoder;
        }
        pp = &(*pp)->next;
    }
    return NULL;
}

SNACKA_API VaDecoderHandle va_decoder_create(void) {
    VaapiDecoder* decoder = vaapi_decoder_create();
    if (!decoder) {
        return NULL;
    }

    pthread_mutex_lock(&s_mutex);
    add_instance(decoder, decoder);
    pthread_mutex_unlock(&s_mutex);

    return decoder;
}

SNACKA_API void va_decoder_destroy(VaDecoderHandle handle) {
    if (!handle) return;

    pthread_mutex_lock(&s_mutex);
    VaapiDecoder* decoder = remove_instance(handle);
    pthread_mutex_unlock(&s_mutex);

    if (decoder) {
        vaapi_decoder_destroy(decoder);
    }
}

SNACKA_API bool va_decoder_initialize(
    VaDecoderHandle handle,
    int width,
    int height,
    const uint8_t* spsData,
    int spsLength,
    const uint8_t* ppsData,
    int ppsLength
) {
    if (!handle) return false;

    pthread_mutex_lock(&s_mutex);
    VaapiDecoder* decoder = find_decoder(handle);
    pthread_mutex_unlock(&s_mutex);

    if (!decoder) return false;

    return vaapi_decoder_initialize(decoder, width, height, spsData, spsLength, ppsData, ppsLength);
}

SNACKA_API bool va_decoder_decode_and_render(
    VaDecoderHandle handle,
    const uint8_t* nalData,
    int nalLength,
    bool isKeyframe
) {
    if (!handle) return false;

    pthread_mutex_lock(&s_mutex);
    VaapiDecoder* decoder = find_decoder(handle);
    pthread_mutex_unlock(&s_mutex);

    if (!decoder) return false;

    return vaapi_decoder_decode_and_render(decoder, nalData, nalLength, isKeyframe);
}

SNACKA_API void* va_decoder_get_view(VaDecoderHandle handle) {
    if (!handle) return NULL;

    pthread_mutex_lock(&s_mutex);
    VaapiDecoder* decoder = find_decoder(handle);
    pthread_mutex_unlock(&s_mutex);

    if (!decoder) return NULL;

    return vaapi_decoder_get_view(decoder);
}

SNACKA_API void va_decoder_set_display_size(
    VaDecoderHandle handle,
    int width,
    int height
) {
    if (!handle) return;

    pthread_mutex_lock(&s_mutex);
    VaapiDecoder* decoder = find_decoder(handle);
    pthread_mutex_unlock(&s_mutex);

    if (!decoder) return;

    vaapi_decoder_set_display_size(decoder, width, height);
}

SNACKA_API bool va_decoder_is_available(void) {
    return vaapi_decoder_is_available();
}
