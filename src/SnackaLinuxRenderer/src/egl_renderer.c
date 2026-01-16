#include "egl_renderer.h"
#include "x11_window.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// NV12 to RGB vertex shader (GLSL ES 2.0)
static const char* s_vertex_shader_src =
    "#version 100\n"
    "attribute vec4 a_position;\n"
    "attribute vec2 a_texCoord;\n"
    "varying vec2 v_texCoord;\n"
    "void main() {\n"
    "    gl_Position = a_position;\n"
    "    v_texCoord = a_texCoord;\n"
    "}\n";

// NV12 to RGB fragment shader (GLSL ES 2.0)
static const char* s_fragment_shader_src =
    "#version 100\n"
    "precision mediump float;\n"
    "varying vec2 v_texCoord;\n"
    "uniform sampler2D y_texture;\n"
    "uniform sampler2D uv_texture;\n"
    "void main() {\n"
    "    float y = texture2D(y_texture, v_texCoord).r;\n"
    "    vec2 uv = texture2D(uv_texture, v_texCoord).rg;\n"
    "    // BT.601 video range conversion\n"
    "    y = (y - 0.0625) * 1.164;\n"
    "    float u = uv.r - 0.5;\n"
    "    float v = uv.g - 0.5;\n"
    "    float r = y + 1.596 * v;\n"
    "    float g = y - 0.391 * u - 0.813 * v;\n"
    "    float b = y + 2.018 * u;\n"
    "    gl_FragColor = vec4(clamp(r, 0.0, 1.0), clamp(g, 0.0, 1.0), clamp(b, 0.0, 1.0), 1.0);\n"
    "}\n";

// Fullscreen quad vertices
static const float s_quad_vertices[] = {
    // position    // texCoord
    -1.0f, -1.0f,  0.0f, 1.0f,  // Bottom-left
    -1.0f,  1.0f,  0.0f, 0.0f,  // Top-left
     1.0f, -1.0f,  1.0f, 1.0f,  // Bottom-right
     1.0f,  1.0f,  1.0f, 0.0f,  // Top-right
};

// Function pointers for EGL extensions
typedef EGLImageKHR (EGLAPIENTRYP PFNEGLCREATEIMAGEKHRPROC)(EGLDisplay, EGLContext, EGLenum, EGLClientBuffer, const EGLint*);
typedef EGLBoolean (EGLAPIENTRYP PFNEGLDESTROYIMAGEKHRPROC)(EGLDisplay, EGLImageKHR);
typedef void (GL_APIENTRYP PFNGLEGLIMAGETARGETTEXTURE2DOESPROC)(GLenum, void*);

static PFNEGLCREATEIMAGEKHRPROC s_eglCreateImageKHR = NULL;
static PFNEGLDESTROYIMAGEKHRPROC s_eglDestroyImageKHR = NULL;
static PFNGLEGLIMAGETARGETTEXTURE2DOESPROC s_glEGLImageTargetTexture2DOES = NULL;

static GLuint compile_shader(GLenum type, const char* source) {
    GLuint shader = glCreateShader(type);
    glShaderSource(shader, 1, &source, NULL);
    glCompileShader(shader);

    GLint compiled;
    glGetShaderiv(shader, GL_COMPILE_STATUS, &compiled);
    if (!compiled) {
        char info[512];
        glGetShaderInfoLog(shader, sizeof(info), NULL, info);
        fprintf(stderr, "EglRenderer: Shader compile error: %s\n", info);
        glDeleteShader(shader);
        return 0;
    }

    return shader;
}

static GLuint create_program(const char* vertex_src, const char* fragment_src) {
    GLuint vs = compile_shader(GL_VERTEX_SHADER, vertex_src);
    if (!vs) return 0;

    GLuint fs = compile_shader(GL_FRAGMENT_SHADER, fragment_src);
    if (!fs) {
        glDeleteShader(vs);
        return 0;
    }

    GLuint program = glCreateProgram();
    glAttachShader(program, vs);
    glAttachShader(program, fs);
    glLinkProgram(program);

    glDeleteShader(vs);
    glDeleteShader(fs);

    GLint linked;
    glGetProgramiv(program, GL_LINK_STATUS, &linked);
    if (!linked) {
        char info[512];
        glGetProgramInfoLog(program, sizeof(info), NULL, info);
        fprintf(stderr, "EglRenderer: Program link error: %s\n", info);
        glDeleteProgram(program);
        return 0;
    }

    return program;
}

EglRenderer* egl_renderer_create(Display* x_display) {
    EglRenderer* renderer = (EglRenderer*)calloc(1, sizeof(EglRenderer));
    if (!renderer) {
        return NULL;
    }

    renderer->x_display = x_display;
    return renderer;
}

void egl_renderer_destroy(EglRenderer* renderer) {
    if (!renderer) return;

    if (renderer->initialized) {
        // Make context current for cleanup
        eglMakeCurrent(renderer->egl_display, renderer->egl_surface,
                       renderer->egl_surface, renderer->egl_context);

        // Delete GL resources
        if (renderer->gl_program) glDeleteProgram(renderer->gl_program);
        if (renderer->y_texture) glDeleteTextures(1, &renderer->y_texture);
        if (renderer->uv_texture) glDeleteTextures(1, &renderer->uv_texture);

        // Destroy EGL resources
        eglMakeCurrent(renderer->egl_display, EGL_NO_SURFACE, EGL_NO_SURFACE, EGL_NO_CONTEXT);
        if (renderer->egl_surface) eglDestroySurface(renderer->egl_display, renderer->egl_surface);
        if (renderer->egl_context) eglDestroyContext(renderer->egl_display, renderer->egl_context);
        eglTerminate(renderer->egl_display);
    }

    // Destroy X11 window
    if (renderer->x_window) {
        x11_destroy_overlay_window(renderer->x_display, renderer->x_window);
    }

    free(renderer);
}

bool egl_renderer_initialize(EglRenderer* renderer, int width, int height) {
    if (!renderer || renderer->initialized) {
        return false;
    }

    renderer->width = width;
    renderer->height = height;

    // Create overlay window
    renderer->x_window = x11_create_overlay_window(renderer->x_display, width, height);
    if (!renderer->x_window) {
        fprintf(stderr, "EglRenderer: Failed to create overlay window\n");
        return false;
    }

    // Get EGL display
    renderer->egl_display = eglGetDisplay((EGLNativeDisplayType)renderer->x_display);
    if (renderer->egl_display == EGL_NO_DISPLAY) {
        fprintf(stderr, "EglRenderer: eglGetDisplay failed\n");
        return false;
    }

    // Initialize EGL
    EGLint major, minor;
    if (!eglInitialize(renderer->egl_display, &major, &minor)) {
        fprintf(stderr, "EglRenderer: eglInitialize failed\n");
        return false;
    }

    printf("EglRenderer: EGL version %d.%d\n", major, minor);

    // Choose config
    EGLint config_attribs[] = {
        EGL_SURFACE_TYPE, EGL_WINDOW_BIT,
        EGL_RED_SIZE, 8,
        EGL_GREEN_SIZE, 8,
        EGL_BLUE_SIZE, 8,
        EGL_ALPHA_SIZE, 8,
        EGL_RENDERABLE_TYPE, EGL_OPENGL_ES2_BIT,
        EGL_NONE
    };

    EGLint num_configs;
    if (!eglChooseConfig(renderer->egl_display, config_attribs, &renderer->egl_config, 1, &num_configs) || num_configs == 0) {
        fprintf(stderr, "EglRenderer: eglChooseConfig failed\n");
        return false;
    }

    // Create context
    EGLint context_attribs[] = {
        EGL_CONTEXT_CLIENT_VERSION, 2,
        EGL_NONE
    };

    renderer->egl_context = eglCreateContext(renderer->egl_display, renderer->egl_config, EGL_NO_CONTEXT, context_attribs);
    if (renderer->egl_context == EGL_NO_CONTEXT) {
        fprintf(stderr, "EglRenderer: eglCreateContext failed\n");
        return false;
    }

    // Create window surface
    renderer->egl_surface = eglCreateWindowSurface(renderer->egl_display, renderer->egl_config,
                                                    (EGLNativeWindowType)renderer->x_window, NULL);
    if (renderer->egl_surface == EGL_NO_SURFACE) {
        fprintf(stderr, "EglRenderer: eglCreateWindowSurface failed\n");
        return false;
    }

    // Make context current
    if (!eglMakeCurrent(renderer->egl_display, renderer->egl_surface, renderer->egl_surface, renderer->egl_context)) {
        fprintf(stderr, "EglRenderer: eglMakeCurrent failed\n");
        return false;
    }

    // Get extension function pointers
    s_eglCreateImageKHR = (PFNEGLCREATEIMAGEKHRPROC)eglGetProcAddress("eglCreateImageKHR");
    s_eglDestroyImageKHR = (PFNEGLDESTROYIMAGEKHRPROC)eglGetProcAddress("eglDestroyImageKHR");
    s_glEGLImageTargetTexture2DOES = (PFNGLEGLIMAGETARGETTEXTURE2DOESPROC)eglGetProcAddress("glEGLImageTargetTexture2DOES");

    if (!s_eglCreateImageKHR || !s_eglDestroyImageKHR || !s_glEGLImageTargetTexture2DOES) {
        fprintf(stderr, "EglRenderer: EGL extensions not available\n");
        // Continue anyway - we'll fall back to vaPutSurface
    }

    // Create shader program
    renderer->gl_program = create_program(s_vertex_shader_src, s_fragment_shader_src);
    if (!renderer->gl_program) {
        fprintf(stderr, "EglRenderer: Failed to create shader program\n");
        return false;
    }

    // Get uniform locations
    renderer->y_texture_loc = glGetUniformLocation(renderer->gl_program, "y_texture");
    renderer->uv_texture_loc = glGetUniformLocation(renderer->gl_program, "uv_texture");

    // Create textures
    glGenTextures(1, &renderer->y_texture);
    glGenTextures(1, &renderer->uv_texture);

    // Setup textures
    glBindTexture(GL_TEXTURE_2D, renderer->y_texture);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

    glBindTexture(GL_TEXTURE_2D, renderer->uv_texture);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

    // Show window
    x11_show_window(renderer->x_display, renderer->x_window);

    renderer->initialized = true;
    printf("EglRenderer: Initialized %dx%d\n", width, height);
    return true;
}

bool egl_renderer_render_surface(
    EglRenderer* renderer,
    VADisplay va_display,
    VASurfaceID surface
) {
    if (!renderer || !renderer->initialized) {
        return false;
    }

    // Make context current
    if (!eglMakeCurrent(renderer->egl_display, renderer->egl_surface, renderer->egl_surface, renderer->egl_context)) {
        return false;
    }

    // Try DMA-BUF export for zero-copy rendering
    VADRMPRIMESurfaceDescriptor prime_desc;
    VAStatus status = vaExportSurfaceHandle(
        va_display,
        surface,
        VA_SURFACE_ATTRIB_MEM_TYPE_DRM_PRIME_2,
        VA_EXPORT_SURFACE_READ_ONLY | VA_EXPORT_SURFACE_COMPOSED_LAYERS,
        &prime_desc
    );

    if (status == VA_STATUS_SUCCESS && s_eglCreateImageKHR && s_glEGLImageTargetTexture2DOES) {
        // Create EGL images from DMA-BUF

        // Y plane
        EGLint y_attribs[] = {
            EGL_WIDTH, renderer->width,
            EGL_HEIGHT, renderer->height,
            EGL_LINUX_DRM_FOURCC_EXT, DRM_FORMAT_R8,
            EGL_DMA_BUF_PLANE0_FD_EXT, prime_desc.objects[0].fd,
            EGL_DMA_BUF_PLANE0_OFFSET_EXT, prime_desc.layers[0].offset[0],
            EGL_DMA_BUF_PLANE0_PITCH_EXT, prime_desc.layers[0].pitch[0],
            EGL_NONE
        };

        EGLImageKHR y_image = s_eglCreateImageKHR(
            renderer->egl_display,
            EGL_NO_CONTEXT,
            EGL_LINUX_DMA_BUF_EXT,
            NULL,
            y_attribs
        );

        // UV plane
        EGLint uv_attribs[] = {
            EGL_WIDTH, renderer->width / 2,
            EGL_HEIGHT, renderer->height / 2,
            EGL_LINUX_DRM_FOURCC_EXT, DRM_FORMAT_GR88,
            EGL_DMA_BUF_PLANE0_FD_EXT, prime_desc.objects[0].fd,
            EGL_DMA_BUF_PLANE0_OFFSET_EXT, prime_desc.layers[0].offset[1],
            EGL_DMA_BUF_PLANE0_PITCH_EXT, prime_desc.layers[0].pitch[1],
            EGL_NONE
        };

        EGLImageKHR uv_image = s_eglCreateImageKHR(
            renderer->egl_display,
            EGL_NO_CONTEXT,
            EGL_LINUX_DMA_BUF_EXT,
            NULL,
            uv_attribs
        );

        if (y_image && uv_image) {
            // Bind to textures
            glActiveTexture(GL_TEXTURE0);
            glBindTexture(GL_TEXTURE_2D, renderer->y_texture);
            s_glEGLImageTargetTexture2DOES(GL_TEXTURE_2D, y_image);

            glActiveTexture(GL_TEXTURE1);
            glBindTexture(GL_TEXTURE_2D, renderer->uv_texture);
            s_glEGLImageTargetTexture2DOES(GL_TEXTURE_2D, uv_image);

            // Render
            glViewport(0, 0, renderer->width, renderer->height);
            glClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            glClear(GL_COLOR_BUFFER_BIT);

            glUseProgram(renderer->gl_program);
            glUniform1i(renderer->y_texture_loc, 0);
            glUniform1i(renderer->uv_texture_loc, 1);

            // Draw fullscreen quad
            GLint pos_loc = glGetAttribLocation(renderer->gl_program, "a_position");
            GLint tex_loc = glGetAttribLocation(renderer->gl_program, "a_texCoord");

            glVertexAttribPointer(pos_loc, 2, GL_FLOAT, GL_FALSE, 4 * sizeof(float), s_quad_vertices);
            glEnableVertexAttribArray(pos_loc);

            glVertexAttribPointer(tex_loc, 2, GL_FLOAT, GL_FALSE, 4 * sizeof(float), s_quad_vertices + 2);
            glEnableVertexAttribArray(tex_loc);

            glDrawArrays(GL_TRIANGLE_STRIP, 0, 4);

            eglSwapBuffers(renderer->egl_display, renderer->egl_surface);
        }

        // Cleanup
        if (y_image) s_eglDestroyImageKHR(renderer->egl_display, y_image);
        if (uv_image) s_eglDestroyImageKHR(renderer->egl_display, uv_image);

        // Close DMA-BUF fds
        for (uint32_t i = 0; i < prime_desc.num_objects; i++) {
            close(prime_desc.objects[i].fd);
        }

        return true;
    }

    // Fallback: Use vaPutSurface (not zero-copy, but works everywhere)
    status = vaPutSurface(
        va_display,
        surface,
        renderer->x_window,
        0, 0, renderer->width, renderer->height,
        0, 0, renderer->width, renderer->height,
        NULL, 0,
        VA_FRAME_PICTURE
    );

    return status == VA_STATUS_SUCCESS;
}

Window egl_renderer_get_window(EglRenderer* renderer) {
    if (!renderer) return 0;
    return renderer->x_window;
}

void egl_renderer_set_display_size(EglRenderer* renderer, int width, int height) {
    if (!renderer || (renderer->width == width && renderer->height == height)) {
        return;
    }

    renderer->width = width;
    renderer->height = height;

    if (renderer->x_window) {
        x11_set_window_geometry(renderer->x_display, renderer->x_window, 0, 0, width, height);
    }
}
