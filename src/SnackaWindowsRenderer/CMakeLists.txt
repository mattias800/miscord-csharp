cmake_minimum_required(VERSION 3.20)
project(SnackaWindowsRenderer LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Windows 10+ required for Media Foundation hardware acceleration
add_definitions(-D_WIN32_WINNT=0x0A00 -DWINVER=0x0A00 -DUNICODE -D_UNICODE)

# Enable exception handling
if(MSVC)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /EHsc")
endif()

add_library(SnackaWindowsRenderer SHARED
    src/CApi.cpp
    src/MediaFoundationDecoder.cpp
    src/D3D11Renderer.cpp
)

# Export symbols
target_compile_definitions(SnackaWindowsRenderer PRIVATE SNACKA_RENDERER_EXPORTS)

# Link required Windows libraries
target_link_libraries(SnackaWindowsRenderer PRIVATE
    # Media Foundation for H.264 decoding
    mf
    mfplat
    mfuuid
    mfreadwrite
    # Direct3D 11 for rendering
    d3d11
    dxgi
    d3dcompiler
    # Windows utilities
    ole32
    uuid
)

# Include directories
target_include_directories(SnackaWindowsRenderer PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# Output to a predictable location
set_target_properties(SnackaWindowsRenderer PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin"
)

# Install target
install(TARGETS SnackaWindowsRenderer
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
)
