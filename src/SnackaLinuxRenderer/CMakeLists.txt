cmake_minimum_required(VERSION 3.20)
project(SnackaLinuxRenderer LANGUAGES C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Find required packages
find_package(PkgConfig REQUIRED)
pkg_check_modules(LIBVA REQUIRED libva libva-drm libva-x11)
pkg_check_modules(EGL REQUIRED egl)
pkg_check_modules(GL REQUIRED gl glesv2)
pkg_check_modules(X11 REQUIRED x11 xfixes)
pkg_check_modules(DRM REQUIRED libdrm)

add_library(SnackaLinuxRenderer SHARED
    src/capi.c
    src/vaapi_decoder.c
    src/egl_renderer.c
    src/x11_window.c
)

target_include_directories(SnackaLinuxRenderer PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${LIBVA_INCLUDE_DIRS}
    ${EGL_INCLUDE_DIRS}
    ${GL_INCLUDE_DIRS}
    ${X11_INCLUDE_DIRS}
    ${DRM_INCLUDE_DIRS}
)

target_link_libraries(SnackaLinuxRenderer PRIVATE
    ${LIBVA_LIBRARIES}
    ${EGL_LIBRARIES}
    ${GL_LIBRARIES}
    ${X11_LIBRARIES}
    ${DRM_LIBRARIES}
    pthread
    m
)

target_compile_options(SnackaLinuxRenderer PRIVATE
    ${LIBVA_CFLAGS_OTHER}
    ${EGL_CFLAGS_OTHER}
    ${GL_CFLAGS_OTHER}
    ${X11_CFLAGS_OTHER}
    ${DRM_CFLAGS_OTHER}
    -Wall
    -Wextra
    -fvisibility=hidden
)

# Export symbols
target_compile_definitions(SnackaLinuxRenderer PRIVATE SNACKA_RENDERER_EXPORTS)

# Version info
set_target_properties(SnackaLinuxRenderer PROPERTIES
    VERSION 1.0.0
    SOVERSION 1
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin"
)

# Install target
install(TARGETS SnackaLinuxRenderer
    LIBRARY DESTINATION lib
)
