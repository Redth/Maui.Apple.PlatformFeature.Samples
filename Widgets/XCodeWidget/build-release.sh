#!/bin/bash
# Build the Widget Extension for both device and simulator.
# Run this from the XCodeWidget/ directory.
#
# Prerequisites:
#   1. Open XCodeWidget.xcodeproj in Xcode at least once to set up signing
#   2. Ensure the Widget Extension target is named "SimpleWidgetExtension"
#
# Output goes to XReleases/ (prefixed with X to avoid .gitignore exclusion)

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

echo "🧹 Cleaning previous builds..."
rm -Rf XReleases

echo "📱 Building for iOS device (iphoneos)..."
xcodebuild -project XCodeWidget.xcodeproj \
    -scheme "SimpleWidgetExtension" \
    -configuration Release \
    -sdk iphoneos \
    BUILD_DIR="$(pwd)/XReleases" clean build

echo "🖥️  Building for iOS simulator (iphonesimulator)..."
xcodebuild -project XCodeWidget.xcodeproj \
    -scheme "SimpleWidgetExtension" \
    -configuration Release \
    -sdk iphonesimulator \
    BUILD_DIR="$(pwd)/XReleases" clean build

echo ""
echo "✅ Build complete! Output:"
echo "   Device:    XReleases/Release-iphoneos/SimpleWidgetExtension.appex"
echo "   Simulator: XReleases/Release-iphonesimulator/SimpleWidgetExtension.appex"
echo ""
echo "📋 Next step: Copy the output to the MAUI project:"
echo "   cp -R XReleases/Release-iphoneos ../Platforms/iOS/WidgetExtensions/"
echo "   cp -R XReleases/Release-iphonesimulator ../Platforms/iOS/WidgetExtensions/"
