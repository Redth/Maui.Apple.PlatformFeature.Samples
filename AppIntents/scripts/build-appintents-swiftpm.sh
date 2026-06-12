#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APPINTENTS_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PACKAGE_DIR="$APPINTENTS_DIR/MauiAppIntentsSample.AppIntents"

SCHEME="${SCHEME:-MauiAppIntentsSampleIntents}"
CONFIGURATION="${CONFIGURATION:-Debug}"
BUILD_DIR="${BUILD_DIR:-$APPINTENTS_DIR/artifacts/swiftpm-appintents}"

if [[ "$BUILD_DIR" != /* ]]; then
  BUILD_DIR="$APPINTENTS_DIR/$BUILD_DIR"
fi

if [[ "$BUILD_DIR" != "$APPINTENTS_DIR"/artifacts/* ]]; then
  echo "BUILD_DIR must be under $APPINTENTS_DIR/artifacts" >&2
  exit 2
fi

ARCHIVES_DIR="$BUILD_DIR/archives"
XFRAMEWORK_DIR="$BUILD_DIR/xcframeworks"
XFRAMEWORK_PATH="$XFRAMEWORK_DIR/$SCHEME.xcframework"
METADATA_PATH="$BUILD_DIR/Metadata.appintents"

rm -rf "$BUILD_DIR"
mkdir -p "$ARCHIVES_DIR" "$XFRAMEWORK_DIR"

archive_platform() {
  local name="$1"
  local sdk="$2"
  local destination="$3"
  local archive_path="$ARCHIVES_DIR/$SCHEME-$name.xcarchive"
  local derived_data="$BUILD_DIR/DerivedData-$name"
  local log_path="$BUILD_DIR/xcodebuild-$name.log"

  echo "=== Archiving $SCHEME for $name ==="
  (
    cd "$PACKAGE_DIR"
    xcodebuild \
      -scheme "$SCHEME" \
      -configuration "$CONFIGURATION" \
      -sdk "$sdk" \
      -destination "$destination" \
      -derivedDataPath "$derived_data" \
      CODE_SIGNING_ALLOWED=NO \
      SKIP_INSTALL=NO \
      BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
      archive -archivePath "$archive_path"
  ) | tee "$log_path"
}

archive_platform "ios" "iphoneos" "generic/platform=iOS"
archive_platform "iossimulator" "iphonesimulator" "generic/platform=iOS Simulator"

DEVICE_FRAMEWORK="$(find "$ARCHIVES_DIR/$SCHEME-ios.xcarchive/Products" -name "$SCHEME.framework" -type d | head -1)"
SIMULATOR_FRAMEWORK="$(find "$ARCHIVES_DIR/$SCHEME-iossimulator.xcarchive/Products" -name "$SCHEME.framework" -type d | head -1)"

if [[ -z "$DEVICE_FRAMEWORK" || -z "$SIMULATOR_FRAMEWORK" ]]; then
  echo "Could not find archived frameworks." >&2
  exit 3
fi

echo "=== Creating xcframework ==="
xcodebuild -create-xcframework \
  -framework "$DEVICE_FRAMEWORK" \
  -framework "$SIMULATOR_FRAMEWORK" \
  -output "$XFRAMEWORK_PATH" | tee "$BUILD_DIR/xcodebuild-create-xcframework.log"

METADATA_SOURCE="$(find "$ARCHIVES_DIR/$SCHEME-ios.xcarchive/Products" -path "*/Metadata.appintents" -type d | head -1)"

if [[ -z "$METADATA_SOURCE" ]]; then
  echo "Could not find Metadata.appintents in the iOS archive." >&2
  exit 4
fi

cp -R "$METADATA_SOURCE" "$METADATA_PATH"

echo "=== SwiftPM App Intents build complete ==="
echo "xcframework: $XFRAMEWORK_PATH"
echo "metadata:    $METADATA_PATH"
