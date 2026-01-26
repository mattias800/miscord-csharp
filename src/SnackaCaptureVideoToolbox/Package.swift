// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "SnackaCaptureVideoToolbox",
    platforms: [
        .macOS(.v13)  // ScreenCaptureKit audio requires macOS 13+
    ],
    products: [
        .executable(name: "SnackaCaptureVideoToolbox", targets: ["SnackaCaptureVideoToolbox"])
    ],
    dependencies: [
        // Pin to 1.3.x to avoid Swift 6.0 features in 1.5+
        .package(url: "https://github.com/apple/swift-argument-parser.git", .upToNextMinor(from: "1.3.0"))
    ],
    targets: [
        // RNNoise C library for noise suppression
        .target(
            name: "CRNNoise",
            path: "Sources/CRNNoise",
            sources: [
                "denoise.c",
                "rnn.c",
                "nnet.c",
                "nnet_default.c",
                "rnnoise_data.c",
                "rnnoise_tables.c",
                "pitch.c",
                "kiss_fft.c",
                "celt_lpc.c",
                "parse_lpcnet_weights.c"
            ],
            publicHeadersPath: "include",
            cSettings: [
                .define("HAVE_STDINT_H"),
                .headerSearchPath("."),
                .headerSearchPath("x86")
            ]
        ),
        .executableTarget(
            name: "SnackaCaptureVideoToolbox",
            dependencies: [
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
                "CRNNoise"
            ],
            linkerSettings: [
                .linkedFramework("ScreenCaptureKit"),
                .linkedFramework("CoreMedia"),
                .linkedFramework("CoreVideo"),
                .linkedFramework("AVFoundation"),
                .linkedFramework("VideoToolbox"),
                .linkedFramework("AppKit")
            ]
        )
    ]
)
