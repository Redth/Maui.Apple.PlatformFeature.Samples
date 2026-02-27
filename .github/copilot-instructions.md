# Copilot Instructions

## Repository Overview

This repository contains **sample projects and Copilot agent skills** for integrating Apple platform features into .NET MAUI iOS apps. It is not a single app — it's a collection of self-contained samples, each in its own directory with its own solution file.

| Directory | What It Is | Solution |
|-----------|-----------|----------|
| `AppIntents/` | Siri App Intents sample (Swift ↔ C# bridge) | `MauiAppIntentsSample.slnx` |
| `Widgets/` | iOS WidgetKit home screen widget sample | `MauiAppleWidgets.slnx` |
| `.agents/skills/` | Copilot agent skill definitions (not buildable code) | — |

## Build Commands

All builds require **macOS**, **Xcode 16+**, and **.NET 10 SDK** with the MAUI workload.

### App Intents Sample

```bash
# Build for device
dotnet build AppIntents/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios

# Build for simulator (no code signing)
dotnet build AppIntents/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=""

# Or use the Makefile
cd AppIntents && make sim

# Clean
dotnet clean AppIntents/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios
```

The Swift framework builds **automatically** via `<XcodeProject>` MSBuild integration — no separate xcodebuild step needed.

### Widgets Sample

```bash
# Build for simulator (widget extension builds automatically via MSBuild target)
dotnet build Widgets/MauiAppleWidgets.csproj -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignRequireProvisioningProfile=false

# Skip widget rebuild when only C# changed
dotnet build Widgets/MauiAppleWidgets.csproj -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignRequireProvisioningProfile=false -p:SkipWidgetBuild=true

# Build for device
dotnet build Widgets/MauiAppleWidgets.csproj -f net10.0-ios
```

After simulator builds, the app must be **re-signed** to restore the App Group entitlement (the MAUI build strips it when `CodesignRequireProvisioningProfile=false`):

```bash
APP_PATH=$(find Widgets/bin/Debug/net10.0-ios/iossimulator-arm64 -name "*.app" -maxdepth 1)
/usr/bin/codesign -v --force --timestamp=none --sign - \
  --entitlements Widgets/Platforms/iOS/Entitlements.plist "$APP_PATH"
```

## Architecture

### App Intents: Three-Project Architecture

App Intents require Swift because iOS uses compile-time metadata extraction (`appintentsmetadataprocessor`) to discover intents — this cannot be done from C#. The architecture keeps all business logic in C# with Swift as a thin declaration layer:

1. **Xcode Framework** (`AppIntents/MauiAppIntentsSample.AppIntents/`) — Swift `AppIntent`, `AppEntity`, `AppEnum` definitions + `@objc` bridge protocol
2. **Binding Library** (`AppIntents/MauiAppIntentsSample.Binding/`) — Maps `@objc` types to C# via `ApiDefinition.cs`, uses `<XcodeProject>` to auto-build the xcframework
3. **MAUI App** (`AppIntents/MauiAppIntentsSample/`) — Implements the bridge protocol in C#, wires it in `AppDelegate.FinishedLaunching`

Data flows: **Siri → Swift `perform()` → `@objc` bridge protocol → C# service → result back through the bridge → Siri dialog**

### Widgets: MAUI App + Swift Extension

Widgets are separate processes that cannot share memory with the host app:

1. **MAUI App** (`Widgets/`) — Writes JSON to App Group shared container, triggers WidgetKit refresh
2. **Swift Widget Extension** (`Widgets/XCodeWidget/`) — Reads JSON from shared container, renders SwiftUI view

Communication uses **JSON files in the App Group container** (not UserDefaults — it's unreliable for cross-process communication). Deep links handle widget-tap-to-app navigation.

## Key Conventions

### Swift ↔ C# Bridge Rules

- Always use `@objc(ClassName)` on Swift classes/protocols — without it, Swift name-mangles and the linker can't find the symbols
- No nullable value types across the bridge — use sentinel values (`-1` for nil int, `""` for nil string). `Date?` is fine (reference type in ObjC)
- In `ApiDefinition.cs`, use `[NullAllowed]` instead of C# `?` nullable syntax — the binding generator doesn't support `#nullable`

### Widget Communication

- Use **file-based I/O** via `NSFileManager.GetContainerUrl()` (C#) / `FileManager.containerURL(forSecurityApplicationGroupIdentifier:)` (Swift) — never UserDefaults for cross-process data
- Widget bundle ID must be a child of the app bundle ID (e.g., `com.example.app.widgetextension`)
- Entitlements plist files must use **LF line endings** (not CRLF)

### Build System

- `Metadata.appintents` must end up in the app bundle or iOS won't discover intents — MSBuild targets handle this, but verify with `ls path/to/App.app/Metadata.appintents/` if intents aren't appearing
- `$(AppBundleDir)` may not end with `/` — always write `$(AppBundleDir)/` with an explicit separator
- The Widgets project uses `xcodegen` to generate the `.xcodeproj` from `project.yml` — the MSBuild target auto-runs this if needed

## Agent Skills

The `.agents/skills/` directory contains two Copilot agent skills with detailed implementation guides and code templates:

- **`maui-ios-appintents`** — Step-by-step guide for adding Siri/Shortcuts/App Intents to a MAUI app, with Swift and C# code templates in `references/`
- **`maui-ios-widget`** — Step-by-step guide for adding iOS home screen widgets to a MAUI app, with Swift and C# code templates in `references/`

When working on App Intents or Widgets features, read the relevant `SKILL.md` and its `references/` files for complete code patterns.
