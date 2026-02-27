---
name: maui-ios-widget
description: "Add iOS home screen widgets to .NET MAUI apps with full bidirectional communication between the app and the widget. Use this skill whenever the user mentions iOS widgets, WidgetKit, widget extensions, home screen widgets, lock screen widgets, or wants to display app data on the iOS home screen from a .NET MAUI or Xamarin app. Also trigger when the user asks about sharing data between a MAUI app and a native iOS extension, using App Groups with MAUI, or embedding an .appex in a MAUI build. This skill covers the complete workflow: creating the Swift/Xcode widget extension project, setting up the shared data layer (JSON files in the App Group container), configuring the MAUI .csproj for widget embedding, implementing deep link communication, building interactive widgets with AppIntents, and wiring everything together. Even if the user just says something like 'I want my app to show a counter on the home screen' or 'can I add a widget to my MAUI app', this skill is the right one."
---

# .NET MAUI iOS Widget Skill

This skill guides you through adding an iOS Widget Extension to a .NET MAUI app with robust bidirectional communication between the C# app code and the Swift widget code.

## Background: How iOS Widgets Work with MAUI

iOS widgets are **standalone app extensions** — they're separate binaries that run in their own process. They cannot directly call into or share memory with the host app. This means:

1. The widget must be written in **Swift** and built via **Xcode/xcodebuild**
2. The compiled widget (an `.appex` bundle) gets embedded into the MAUI app at build time
3. All communication happens through **JSON files in the App Group shared container** — both the app and the widget can read/write files to this shared directory
4. The MAUI app signals the widget to refresh via **WidgetKit** (using a .NET binding NuGet package)
5. The widget can signal the app via **deep links** (tapping the widget) or by writing data to the shared container

> **Why files instead of UserDefaults?** `UserDefaults(suiteName:)` can resolve to different backing plist files for the MAUI app process vs. the widget extension process, especially on the simulator or with ad-hoc code signing. The app writes to the App Group container directory, but the widget extension may write to the system-level `Library/Preferences/`. File-based I/O via `NSFileManager.GetContainerUrl()` (C#) / `FileManager.containerURL(forSecurityApplicationGroupIdentifier:)` (Swift) is reliable for cross-process communication. **Do NOT use UserDefaults for cross-process data sharing.**

Understanding this architecture is essential — there's no way around the Swift requirement for the widget itself, but the amount of Swift needed is minimal and follows clear patterns.

## When to Use This Skill

- Adding a widget to an **existing** .NET MAUI app
- Creating a **new** .NET MAUI app that includes a widget
- Setting up **data sharing** between a MAUI app and an iOS extension
- Making a widget **interactive** (buttons that trigger actions)
- Implementing **deep linking** from a widget back to the app

## Assessment: Understand the Starting Point

Before writing any code, determine the user's situation:

1. **New or existing app?** — If new, scaffold a MAUI project first. If existing, we're adding to it.
2. **What data should the widget display?** — A counter, a list, status text, images, etc. This shapes the `WidgetData` model.
3. **Should the widget be interactive?** — Buttons that modify data? Or display-only?
4. **What are the user's identifiers?** — If they have Apple Developer identifiers (Bundle ID, App Group ID), use them. If not, derive sensible defaults from the project name.

Ask these questions, but make reasonable defaults if the user is vague. You can always adjust later.

## Identifier Convention

Derive all identifiers from the app's Bundle ID. For an app with Bundle ID `com.example.myapp`:

| Identifier | Value | Where Used |
|-----------|-------|------------|
| App Bundle ID | `com.example.myapp` | `.csproj`, Xcode project |
| Widget Bundle ID | `com.example.myapp.widgetextension` | Xcode project — **must be a child of the app bundle ID** |
| App Group ID | `group.com.example.myapp` | Entitlements (×2), C# constants, Swift constants |
| URL Scheme | `myapp` (or similar short form) | Info.plist, C# constants, Swift widgetURL |
| Widget Kind | `MyWidget` (descriptive name) | Swift Widget struct, C# RefreshWidget call |

These identifiers must match **exactly** across all files — mismatches cause silent failures. The widget bundle ID **must** be a child of the app bundle ID (e.g., `com.example.myapp.WidgetExtension`) or `simctl install` will fail with "Mismatched bundle IDs".

## Implementation Workflow

Follow these steps in order. Each step references a specific template file in `references/`.

### Step 1: Create the C# Service Layer

Read `references/csharp-templates.md` — Section "Service Layer"

Create these files in the MAUI project:

1. **`Services/WidgetData.cs`** — The shared data model. Customize properties based on what data the widget needs to display. Always keep `Version` and `Extras` for forward compatibility. Use `[JsonPropertyName]` attributes with camelCase names to match the Swift `Codable` side.

2. **`Services/WidgetConstants.cs`** — All identifiers in one place (Group ID, shared filenames, URL scheme, widget kind). Every identifier used in communication lives here. Use file names (e.g., `widget_data_fromapp.json`) not UserDefaults keys.

3. **`Services/IWidgetDataService.cs`** — Interface with four methods: `SendDataToWidget`, `ReadDataFromWidget`, `ClearWidgetIncomingData`, `RefreshWidget`.

4. **`Services/StubWidgetDataService.cs`** — No-op implementation for non-iOS platforms.

5. **`Platforms/iOS/WidgetDataService.cs`** — The real implementation using **file-based I/O** to the App Group shared container. Uses `NSFileManager.DefaultManager.GetContainerUrl()` to get the container directory, writes/reads JSON files directly. Uses `WidgetKit.WidgetCenterProxy` for triggering refreshes.

### Step 2: Create the Entitlements and Update Info.plist

Read `references/project-config.md` — Section "Entitlements and Plists"

Create these files:

1. **`Platforms/iOS/Entitlements.plist`** — App Group entitlement for the MAUI app
2. **`Platforms/iOS/Entitlements.WidgetExtension.plist`** — App Group entitlement for the widget (same Group ID)

**Critical**: Both entitlements files MUST use **LF line endings** (Unix-style), not CRLF. This is a known build-breaking issue — the MAUI build reads these files and silently fails with CRLF. After creating them, run:
```bash
sed -i '' 's/\r$//' Platforms/iOS/Entitlements.plist Platforms/iOS/Entitlements.WidgetExtension.plist
```

Update **`Platforms/iOS/Info.plist`** to register the custom URL scheme for deep linking (add `CFBundleURLTypes`).

### Step 3: Wire Up Deep Link Handling and App Lifecycle

Read `references/csharp-templates.md` — Section "App Integration"

Modify these existing files:

1. **`Platforms/iOS/AppDelegate.cs`** — Override `OpenUrl` to intercept the custom URL scheme and route to `App.HandleWidgetUrl`.

2. **`App.xaml.cs`** — Add `HandleWidgetUrl` static method that parses the URL query string and dispatches data to the active page. Add a `Resumed` handler on the window to reload widget data when the app comes back from background.

3. **`MauiProgram.cs`** — Register `IWidgetDataService` via DI: the iOS implementation behind `#if IOS`, the stub otherwise. Register pages that use DI as `AddTransient`.

### Step 4: Build the MainPage (or integrate into existing pages)

Read `references/csharp-templates.md` — Section "MainPage Example"

If the user has an existing page, integrate the widget data service into it. If starting fresh, create a page that demonstrates:
- Displaying data that syncs with the widget
- Buttons that update the data and trigger a widget refresh
- Reading incoming data from the widget on appear/resume
- Handling deep link data from widget taps

The key pattern: any page that interacts with the widget should:
- Inject `IWidgetDataService` via constructor
- Read incoming data in `OnAppearing` and `OnResumed`
- Write outgoing data and call `RefreshWidget` after any state change

### Step 5: Update the .csproj

Read `references/project-config.md` — Section "Project File Configuration"

Add these iOS-conditional sections:

1. **PropertyGroup**: Set `CodesignEntitlements` to point to the app's Entitlements.plist. **Use forward slashes** in the path (e.g., `Platforms/iOS/Entitlements.plist`).
2. **PackageReference**: Add `WidgetKit.WidgetCenterProxy` NuGet (check NuGet.org for the latest version — look for one matching the app's .NET TFM)
3. **ItemGroup**: Copy `.appex` content files, copy widget entitlements, and declare `AdditionalAppExtensions` to embed the widget at build time
4. **BuildWidgetExtension target**: A custom MSBuild target that automatically builds the Xcode widget project via `xcodebuild` during `dotnet build`, so the widget doesn't need to be built separately. It runs `xcodegen generate` if the `.xcodeproj` doesn't exist but `project.yml` does. Skip with `-p:SkipWidgetBuild=true`.

The `AdditionalAppExtensions` element is the critical piece — it tells the MAUI build system to embed the widget `.appex` into the final app bundle and sign it with the widget's entitlements. The `XcodeProject` MSBuild item group (from dotnet/macios) only supports building frameworks, not `.appex` bundles, which is why we use a custom target instead.

### Step 6: Create the Swift Widget Extension

Read `references/swift-templates.md` — all sections

Create the Xcode project directory (conventionally named `XCodeWidget/` at the repo root) with these Swift files:

1. **`Settings.swift`** — Mirror of `WidgetConstants.cs`. Same Group ID, same filenames, same URL scheme.
2. **`WidgetData.swift`** — Codable struct matching the C# `WidgetData` record. Property names must match the `[JsonPropertyName]` values exactly.
3. **`SharedStorage.swift`** — Reads/writes `WidgetData` as JSON files to/from the App Group container directory using `FileManager.containerURL(forSecurityApplicationGroupIdentifier:)`. **Do NOT use UserDefaults** — use file I/O for reliable cross-process communication.
4. **`SimpleEntry.swift`** — `TimelineEntry` struct with the fields the view needs.
5. **`Provider.swift`** — `AppIntentTimelineProvider` with `placeholder`, `snapshot`, and `timeline` functions. Reads from `SharedStorage`, constructs entries with deep link URLs.
6. **`SimpleWidgetView.swift`** — SwiftUI view. Uses `widgetURL()` for deep linking. If interactive, includes `Button(intent:)` views.
7. **`SimpleWidget.swift`** — Widget configuration declaring the kind, supported sizes, and wiring the provider + view.
8. **`SimpleWidgetBundle.swift`** — `@main` entry point that exposes one or more widgets.
9. **`Intents/ConfigurationAppIntent.swift`** — User-configurable parameters (title, emoji, etc.)
10. **`Intents/IncrementCounterIntent.swift`** (and similar) — AppIntents for interactive buttons. Read current state from `SharedStorage`, modify, write back, call `WidgetCenter.shared.reloadTimelines(ofKind:)`.
11. **`Info.plist`** — Widget extension config with **full CFBundle keys** (CFBundleDevelopmentRegion, CFBundleExecutable, CFBundleIdentifier, CFBundleInfoDictionaryVersion, CFBundleName, CFBundlePackageType, CFBundleShortVersionString, CFBundleVersion, plus the NSExtension dict). A minimal plist will cause the AppIntentsSSUTraining build step to fail.

Also create:
- **`SimpleWidgetExtension.entitlements`** — Same App Group entitlement (LF line endings)
- **`Assets.xcassets/`** — AppIcon and color assets
- **`build-release.sh`** — Shell script to build via `xcodebuild` for both iphoneos and iphonesimulator SDKs

### Step 7: Create the Xcode Project

The `.xcodeproj` can be created two ways:

**Option A: xcodegen (recommended)**

Install xcodegen (`brew install xcodegen`), create a `project.yml` in the `XCodeWidget/` directory, and run `xcodegen generate`. This is faster and more reproducible than manual Xcode setup. See `references/project-config.md` — Section "xcodegen project.yml".

**Option B: Manual Xcode setup**

1. Open Xcode → File → New → Project → App template
2. Product Name: `XCodeWidget`, Bundle ID: matches the MAUI app's Bundle ID
3. Save in the `XCodeWidget/` directory
4. File → New → Target → Widget Extension
5. Product Name: `SimpleWidgetExtension`, Bundle ID: `<app-bundle-id>.widgetextension`
6. Check "Include Configuration App Intent"
7. Delete the auto-generated Swift files from the extension target
8. Add all the Swift files we created to the widget extension target
9. In Signing & Capabilities: add "App Groups" with the Group ID
10. Set Minimum Deployment to iOS 17.0 on both targets
11. Build once to verify: Product → Build

### Step 8: Build and Integrate

The `.csproj` includes a `BuildWidgetExtension` MSBuild target that automatically builds the widget extension as part of `dotnet build`. This means a single command handles everything:

#### For simulator builds:
```bash
dotnet build MyApp.csproj -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignRequireProvisioningProfile=false
```

This automatically:
1. Runs `xcodegen generate` if `.xcodeproj` doesn't exist (but `project.yml` does)
2. Builds the widget with `xcodebuild -quiet`
3. Copies the `.appex` to `Platforms/iOS/WidgetExtensions/`
4. Builds the MAUI app and embeds the widget

Skip the widget build with `-p:SkipWidgetBuild=true` if you only changed C# code.

**Critical post-build step**: The MAUI build generates an empty `.xcent` file when `CodesignRequireProvisioningProfile=false`, stripping the App Group entitlement from the main app. You must re-sign:
```bash
APP_PATH=$(find bin/Debug/net10.0-ios/iossimulator-arm64 -name "*.app" -maxdepth 1)
/usr/bin/codesign -v --force --timestamp=none --sign - \
  --entitlements Platforms/iOS/Entitlements.plist "$APP_PATH"
```

Without this re-signing step, `NSFileManager.GetContainerUrl()` returns null and cross-process communication fails silently.

#### For device builds:
```bash
dotnet build MyApp.csproj -f net10.0-ios
```

The same `BuildWidgetExtension` target handles device builds automatically (detecting simulator vs device from the RuntimeIdentifier).

#### Manual widget build (alternative):

If you prefer to build the widget separately (or need to debug the xcodebuild step):
```bash
cd XCodeWidget
xcodebuild -project XCodeWidget.xcodeproj \
  -target SimpleWidgetExtension \
  -configuration Release \
  -sdk iphonesimulator -arch arm64 \
  CODE_SIGN_IDENTITY="-" CODE_SIGNING_REQUIRED=NO CODE_SIGNING_ALLOWED=NO \
  BUILD_DIR=$(pwd)/build clean build
```

Then copy and build with `-p:SkipWidgetBuild=true`:
```bash
cp -R XCodeWidget/build/Release-iphonesimulator/SimpleWidgetExtension.appex \
  Platforms/iOS/WidgetExtensions/Release-iphonesimulator/
dotnet build MyApp.csproj -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignRequireProvisioningProfile=false -p:SkipWidgetBuild=true
```

## Communication Architecture Summary

| Direction | Mechanism | How It Works |
|-----------|-----------|-------------|
| **App → Widget** | File I/O + WidgetKit | App writes JSON file to App Group container, then calls `ReloadTimeLinesOfKind` to tell iOS to refresh the widget |
| **Widget → App (tap)** | Deep links | Widget sets `widgetURL()` with a custom URL scheme. Tapping opens the app, `AppDelegate.OpenUrl` catches it |
| **Widget → App (interactive)** | AppIntents + File I/O | Widget buttons trigger AppIntents that write JSON files to the container. App reads on next resume |
| **Widget → App (background)** | Silent push notification | Widget AppIntent calls a backend, which sends a silent push to the app (stub — requires backend setup) |

## Adapting the Data Model

The default template uses a counter, but the pattern works for any data:

1. Modify `WidgetData.cs` (C#) and `WidgetData.swift` — add/remove/change properties
2. Keep JSON property names matching between both sides (camelCase)
3. The `Extras` dictionary allows ad-hoc data without schema changes
4. Bump `Version` when making breaking changes to the schema
5. Update `SimpleEntry.swift` to carry the fields the view needs
6. Update `Provider.swift` to read and map the new data
7. Update `SimpleWidgetView.swift` to display the new data

## Widget Size Considerations

- **`.systemSmall`** — Display-only, no interactive buttons (iOS limitation). Good for glanceable data.
- **`.systemMedium`** — Supports interactive buttons. Good balance of content and interaction.
- **`.systemLarge`** — Room for lists, charts, detailed content. Supports interactive elements.
- **`.accessoryCircular`**, **`.accessoryRectangular`**, **`.accessoryInline`** — Lock screen widgets. Minimal content.

Configure supported sizes in the `Widget` struct's `.supportedFamilies()` modifier.

## Reference Files

The `references/` directory contains complete code templates:

- **`references/csharp-templates.md`** — All C# files: WidgetData, IWidgetDataService, WidgetConstants, StubWidgetDataService, WidgetDataService (iOS), AppDelegate, App.xaml.cs, MauiProgram.cs, MainPage
- **`references/swift-templates.md`** — All Swift files: Settings, WidgetData, SharedStorage, SimpleEntry, Provider, SimpleWidgetView, SimpleWidget, SimpleWidgetBundle, ConfigurationAppIntent, IncrementCounterIntent, DecrementCounterIntent, SilentNotificationService
- **`references/project-config.md`** — .csproj additions, Entitlements.plist files, Info.plist URL scheme, widget Info.plist, build-release.sh, xcodegen project.yml, Asset catalog JSON files
- **`references/troubleshooting.md`** — Common pitfalls and their solutions (UserDefaults cross-process bug, line endings, provisioning, re-signing, simulator builds, icon caching, throttling, etc.)

Read the relevant reference file when you reach that step in the workflow. Don't load all of them upfront — they're detailed templates meant for copy-adapt-paste.
