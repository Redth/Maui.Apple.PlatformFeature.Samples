// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MauiAppIntentsSampleIntents",
    platforms: [
        .iOS(.v17)
    ],
    products: [
        .library(
            name: "MauiAppIntentsSampleIntents",
            type: .dynamic,
            targets: ["MauiAppIntentsSampleIntents"]
        )
    ],
    targets: [
        .target(
            name: "MauiAppIntentsSampleIntents",
            path: "Sources",
            linkerSettings: [
                .linkedFramework("AppIntents"),
                .linkedFramework("Foundation")
            ]
        )
    ],
    swiftLanguageModes: [.v5]
)
