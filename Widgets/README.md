# MauiAppleWidgets

A .NET MAUI app with a bundled iOS Widget Extension, demonstrating robust bidirectional communication between the app and the widget.

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    .NET MAUI App (C#)                             │
│                                                                  │
│  MainPage ──► IWidgetDataService ──► JSON file (App Group)       │
│     ▲              │                        │                    │
│     │              ▼                        │                    │
│     │    WidgetKit.ReloadTimelines ─────────┼──► iOS refreshes   │
│     │                                      │       widget       │
│     │    AppDelegate.OpenUrl ◄─────────────┤                    │
│     └──── HandleWidgetUrl (deep link) ◄────┘                    │
└──────────────────────────────────────────────────────────────────┘
                    │  JSON files in App Group container  │
┌──────────────────────────────────────────────────────────────────┐
│                iOS Widget Extension (Swift)                       │
│                                                                  │
│  SharedStorage ──► File I/O (App Group) ──► Provider             │
│       ▲                                          │               │
│       │                                          ▼               │
│  AppIntents (buttons) ◄──── SimpleWidgetView ◄── SimpleEntry     │
│       │                          │                               │
│       ▼                          ▼                               │
│  WidgetCenter.reloadTimelines   widgetURL (deep link to app)     │
└──────────────────────────────────────────────────────────────────┘
```

### Communication Channels

| Direction | Mechanism | Description |
|-----------|-----------|-------------|
| **App → Widget** | JSON file in App Group container + WidgetKit reload | App writes `WidgetData` JSON file, then calls `ReloadTimeLinesOfKind` |
| **Widget → App (tap)** | Deep link via `widgetURL()` | Tapping the widget opens the app with `mauiapplewidgets://widget?counter=N` |
| **Widget → App (interactive)** | AppIntents + file I/O | Widget buttons modify shared state via `SharedStorage` |
| **Widget → App (background)** | Silent push notification (stub) | For server-side flows without opening the app |

### Shared Data Contract

Both sides exchange `WidgetData` JSON objects via files in the App Group shared container:

```json
{
  "version": 1,
  "title": "My Widget",
  "message": "Sent from app",
  "counter": 42,
  "updatedAt": "2026-02-27T02:00:00Z",
  "extras": {}
}
```

**Shared files** (in the App Group container directory):
- `widget_data_fromapp.json` — app writes, widget reads
- `widget_data_fromwidget.json` — widget writes, app reads

> **Why files instead of UserDefaults?** `UserDefaults(suiteName:)` can resolve to different backing plist files for the app vs. the widget extension, especially on the simulator or with ad-hoc code signing. File-based I/O via `NSFileManager.GetContainerUrl()` / `FileManager.containerURL(forSecurityApplicationGroupIdentifier:)` is more reliable for cross-process communication. See [Known Gotchas](#known-gotchas) for details.

## Prerequisites

### Apple Developer Console Setup (Device Builds)

For device builds, create these identifiers in your [Apple Developer Console](https://developer.apple.com/account):

1. **App Bundle ID**: `com.mauiapplewidgets.app` (or your own)
2. **Widget Bundle ID**: `com.mauiapplewidgets.app.widgetextension` (must be a child of the app bundle ID)
3. **App Group**: `group.com.mauiapplewidgets.app`
   - Enable the App Groups capability on **both** Bundle IDs

For **simulator builds**, provisioning profiles are not required — see the simulator-specific build instructions below.

### Development Requirements

- macOS with Xcode 16+
- .NET 9/10 SDK with MAUI workload
- An iOS device or simulator (iOS 17+)
- [xcodegen](https://github.com/yonaskolb/XcodeGen) (optional — for regenerating the Xcode project)

## Project Structure

```
MauiAppleWidgets/
├── MauiAppleWidgets.slnx
├── MauiAppleWidgets.csproj
├── App.xaml / App.xaml.cs              # Deep link URL handler
├── MainPage.xaml / MainPage.xaml.cs    # Counter UI with widget sync
├── MauiProgram.cs                      # DI registration
├── Services/
│   ├── IWidgetDataService.cs           # Widget communication interface
│   ├── WidgetData.cs                   # Shared data model (JSON)
│   ├── WidgetConstants.cs              # Group ID, filenames, URL scheme
│   └── StubWidgetDataService.cs        # No-op for non-iOS platforms
├── Platforms/iOS/
│   ├── AppDelegate.cs                  # OpenUrl handler for deep links
│   ├── WidgetDataService.cs            # iOS implementation (file I/O + WidgetKit)
│   ├── Info.plist                      # URL scheme registration
│   ├── Entitlements.plist              # App Group for the MAUI app
│   ├── Entitlements.WidgetExtension.plist  # App Group for the widget
│   └── WidgetExtensions/              # Built .appex files go here
│       ├── Release-iphoneos/
│       └── Release-iphonesimulator/
└── XCodeWidget/                        # Xcode project (Swift)
    ├── project.yml                     # xcodegen spec (generates .xcodeproj)
    ├── XCodeWidget.xcodeproj/
    ├── XCodeWidget/                    # Thin host app (not shipped)
    ├── SimpleWidgetExtension/
    │   ├── Settings.swift              # Constants (mirrors WidgetConstants.cs)
    │   ├── WidgetData.swift            # Codable model (mirrors WidgetData.cs)
    │   ├── SharedStorage.swift         # JSON file read/write via App Group container
    │   ├── SimpleEntry.swift           # TimelineEntry
    │   ├── Provider.swift              # AppIntentTimelineProvider
    │   ├── SimpleWidgetView.swift      # SwiftUI view with buttons
    │   ├── SimpleWidget.swift          # Widget configuration
    │   ├── SimpleWidgetBundle.swift    # @main entry point
    │   ├── Info.plist                  # Extension config (full CFBundle keys required)
    │   ├── Intents/
    │   │   ├── ConfigurationAppIntent.swift
    │   │   ├── IncrementCounterIntent.swift
    │   │   └── DecrementCounterIntent.swift
    │   └── Services/
    │       └── SilentNotificationService.swift
    ├── SimpleWidgetExtension.entitlements
    └── build-release.sh
```

## Build & Run

### Option A: Simulator Build (No Apple Developer Account Required)

#### Step 1: Generate the Xcode Project (First Time Only)

Install [xcodegen](https://github.com/yonaskolb/XcodeGen) if needed:

```bash
brew install xcodegen
```

Then generate the `.xcodeproj`:

```bash
cd XCodeWidget
xcodegen generate
```

> **Note**: The `.csproj` includes a `BuildWidgetExtension` MSBuild target that will auto-run `xcodegen generate` if the `.xcodeproj` doesn't exist but `project.yml` does. So this step is optional — `dotnet build` will handle it.

#### Step 2: Build Everything

The MAUI build automatically builds the widget extension via a custom MSBuild target. Just run:

```bash
dotnet build MauiAppleWidgets.csproj -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignRequireProvisioningProfile=false
```

This single command:
1. Runs `xcodegen generate` if `.xcodeproj` doesn't exist (but `project.yml` does)
2. Builds the widget extension with `xcodebuild -quiet`
3. Copies the `.appex` to `Platforms/iOS/WidgetExtensions/Release-iphonesimulator/`
4. Builds the MAUI app and embeds the widget

> To skip the widget build (e.g., if you only changed C# code): `dotnet build ... -p:SkipWidgetBuild=true`

#### Step 3: Re-sign with Entitlements

> **Critical**: The MAUI build generates an empty `.xcent` file when `CodesignRequireProvisioningProfile=false`, which strips the App Group entitlement and breaks cross-process communication.

```bash
APP_PATH=$(find bin/Debug/net10.0-ios/iossimulator-arm64 -name "*.app" -maxdepth 1)
/usr/bin/codesign -v --force --timestamp=none --sign - \
  --entitlements Platforms/iOS/Entitlements.plist "$APP_PATH"
```

#### Step 4: Install and Launch on Simulator

```bash
# Boot a simulator (adjust device ID as needed)
xcrun simctl boot "iPhone 16 Pro"

# Install and launch
xcrun simctl install booted "$APP_PATH"
xcrun simctl launch booted com.companyname.mauiapplewidgets
```

#### Step 5: Add the Widget

On the simulator:
1. Long-press the home screen → tap **+** (top left)
2. Search for "MauiAppleWidgets"
3. Choose the Medium widget size and add it

### Option B: Device Build (Requires Apple Developer Account)

#### Step 1: Create the Xcode Project

Option A: Use `xcodegen generate` from the `XCodeWidget/` directory (uses the provided `project.yml`).

Option B: Create manually in Xcode:
1. Open Xcode → **File → New → Project** → choose **App** template
2. Set Product Name to `XCodeWidget`, Bundle ID to `com.mauiapplewidgets.app`
3. Save inside the `XCodeWidget/` folder
4. **File → New → Target** → choose **Widget Extension**
5. Set Product Name to `SimpleWidgetExtension`
6. Check "Include Configuration App Intent"
7. **Delete** auto-generated Swift files and **add** the files from `SimpleWidgetExtension/`
8. In **Signing & Capabilities**, add "App Groups" with `group.com.mauiapplewidgets.app`
9. Set Minimum Deployment to iOS 17.0 for both targets

#### Step 2: Build the Widget Release

```bash
cd XCodeWidget
./build-release.sh
```

#### Step 3: Copy Widget to MAUI Project

```bash
cp -R XCodeWidget/XReleases/Release-iphoneos Platforms/iOS/WidgetExtensions/
cp -R XCodeWidget/XReleases/Release-iphonesimulator Platforms/iOS/WidgetExtensions/
```

#### Step 4: Build & Run the MAUI App

```bash
dotnet build -t:Run -f net10.0-ios
```

Or open `MauiAppleWidgets.slnx` in your IDE and run on an iOS device.

## Customization Checklist

When adapting this template for your own app, update these values everywhere they appear:

| Value | Files to Update |
|-------|----------------|
| App Bundle ID | `.csproj`, Xcode project settings / `project.yml` |
| Widget Bundle ID | Xcode project settings / `project.yml` (must be a child of app bundle ID) |
| App Group ID (`group.com.mauiapplewidgets.app`) | `WidgetConstants.cs`, `Settings.swift`, all `Entitlements*.plist` files |
| URL Scheme (`mauiapplewidgets`) | `WidgetConstants.cs`, `Settings.swift`, `Info.plist` |
| Widget Kind (`SimpleWidget`) | `WidgetConstants.cs`, `Settings.swift`, `SimpleWidget.swift` |

### Extending the Data Model

1. Add properties to `WidgetData.cs` (C#) and `WidgetData.swift` (Swift)
2. Both use JSON serialization — keep property names matching (camelCase)
3. The `extras` dictionary allows ad-hoc data without schema changes
4. Bump the `version` field when making breaking changes

## Known Gotchas

### Critical

1. **UserDefaults is unreliable for cross-process widget communication.** `UserDefaults(suiteName:)` can resolve to different plist files for the MAUI app vs. the widget extension, even when both have the correct App Group entitlement. The MAUI app writes to the App Group container (`Containers/Shared/AppGroup/.../Library/Preferences/`), while the widget extension may write to the system-level preferences (`Library/Preferences/`). **Use file-based I/O** via `NSFileManager.GetContainerUrl()` (C#) / `FileManager.containerURL(forSecurityApplicationGroupIdentifier:)` (Swift) instead.

2. **Widget extension bundle ID must be a child of the app bundle ID.** For example, if the app is `com.mycompany.myapp`, the widget must be `com.mycompany.myapp.WidgetExtension`. Mismatched IDs cause `simctl install` to fail with a "Mismatched bundle IDs" error.

3. **The MAUI build generates an empty `.xcent` when `CodesignRequireProvisioningProfile=false`.** This strips the App Group entitlement from the main app binary, breaking `NSFileManager.GetContainerUrl()`. You must re-sign the app after building (see build instructions above).

### Build & Configuration

4. **Entitlements plist files must use LF line endings.** CRLF causes cryptic build errors.

5. **Widget extension Info.plist needs full CFBundle keys** when `GENERATE_INFOPLIST_FILE` is `false`. A minimal plist (just `NSExtension` key) causes the AppIntentsSSUTraining build step to fail. Include `CFBundleDevelopmentRegion`, `CFBundleExecutable`, `CFBundleIdentifier`, `CFBundleInfoDictionaryVersion`, `CFBundleName`, `CFBundlePackageType`, `CFBundleShortVersionString`, and `CFBundleVersion`.

6. **Build the widget using `-target`, not `-scheme`.** Building with `-scheme` requires a full host app Info.plist. Use `-target SimpleWidgetExtension` instead.

7. **Use `xcodegen` to avoid manual Xcode project creation.** The `project.yml` file in `XCodeWidget/` defines both the host app and widget extension targets. Run `xcodegen generate` to create the `.xcodeproj`.

### Runtime

8. **WidgetKit throttling**: Apple limits how often `reloadTimelines` refreshes the widget. In development it's usually instant; in production there may be delays.

9. **Xcode host app is throwaway**: The `XCodeWidget/XCodeWidget/` app project is required by Xcode but never ships. Only the widget extension target matters.

10. **App Group provisioning**: Both the app AND widget extension need the same App Group enabled in the Apple Developer portal (device builds only).

11. **Don't use `widgetURL()` on a parent container that wraps interactive buttons.** The URL handler can intercept `Button(intent:)` taps. Use `Link(destination:)` on individual non-interactive areas instead.

12. **Use timestamp-based priority in `getBestCounter()`.** If app data always takes priority over widget data, widget button increments get immediately overridden on the next timeline reload. Compare `updatedAt` strings to pick whichever source was updated most recently.

## Full Rebuild & Deploy Script (Simulator)

For convenience, here's the complete rebuild flow. Thanks to the `BuildWidgetExtension` MSBuild target, the widget is built automatically:

```bash
#!/bin/bash
set -e

# 1. Build MAUI app (automatically builds widget extension too)
dotnet build MauiAppleWidgets.csproj -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignRequireProvisioningProfile=false

# 2. Re-sign with entitlements (required for simulator builds)
APP_PATH=$(find bin/Debug/net10.0-ios/iossimulator-arm64 -name "*.app" -maxdepth 1)
/usr/bin/codesign -v --force --timestamp=none --sign - \
  --entitlements Platforms/iOS/Entitlements.plist "$APP_PATH"

# 3. Install and launch
xcrun simctl install booted "$APP_PATH"
xcrun simctl launch booted com.companyname.mauiapplewidgets
```

> **Tip**: To skip the widget build when only C# changed: add `-p:SkipWidgetBuild=true`

## References

- [How to Build iOS Widgets with .NET MAUI](https://devblogs.microsoft.com/dotnet/how-to-build-ios-widgets-with-dotnet-maui/) — Toine de Boer
- [Maui.WidgetExample](https://github.com/Toine-db/Maui.WidgetExample) — Reference implementation
- [WidgetKit.WidgetCenterProxy NuGet](https://www.nuget.org/packages/WidgetKit.WidgetCenterProxy) — .NET binding for WidgetKit
