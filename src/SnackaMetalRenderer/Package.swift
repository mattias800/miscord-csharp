// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "SnackaMetalRenderer",
    platforms: [
        .macOS(.v12)
    ],
    products: [
        .library(
            name: "SnackaMetalRenderer",
            type: .dynamic,
            targets: ["SnackaMetalRenderer"]
        ),
    ],
    targets: [
        .target(
            name: "SnackaMetalRenderer",
            dependencies: [],
            linkerSettings: [
                .linkedFramework("Metal"),
                .linkedFramework("MetalKit"),
                .linkedFramework("AppKit"),
                .linkedFramework("QuartzCore"),
                .linkedFramework("VideoToolbox"),
                .linkedFramework("CoreMedia"),
                .linkedFramework("CoreVideo")
            ]
        ),
    ]
)
