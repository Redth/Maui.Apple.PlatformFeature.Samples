# AGENTS.md

Instructions for AI coding agents working in this repository.

## Repository Structure

This is a **collection of independent samples**, not a single app. Each sample has its own solution file and builds independently.

| Directory | Purpose | Solution |
|-----------|---------|----------|
| `AppIntents/` | Siri App Intents sample (Swift ↔ C# bridge) | `MauiAppIntentsSample.slnx` |
| `Widgets/` | iOS WidgetKit home screen widget sample | `MauiAppleWidgets.slnx` |
| `.agents/skills/` | Agent skill definitions with code templates (not buildable) | — |

## Build Commands

All builds require **macOS**, **Xcode 16+**, and **.NET 10 SDK** with the MAUI workload.

### App Intents

```bash
# Device build (Swift framework builds automatically via <XcodeProject>)
dotnet build AppIntents/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios

# Simulator build
dotnet build AppIntents/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=""

# Makefile shortcut
cd AppIntents && make sim
```

### Widgets

```bash
# Simulator build (widget extension builds automatically via MSBuild target)
dotnet build Widgets/MauiAppleWidgets.csproj -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignRequireProvisioningProfile=false

# Device build
dotnet build Widgets/MauiAppleWidgets.csproj -f net10.0-ios

# Skip widget rebuild when only C# changed
-p:SkipWidgetBuild=true
```

Simulator widget builds require **re-signing** afterward to restore the App Group entitlement:

```bash
APP_PATH=$(find Widgets/bin/Debug/net10.0-ios/iossimulator-arm64 -name "*.app" -maxdepth 1)
/usr/bin/codesign -v --force --timestamp=none --sign - \
  --entitlements Widgets/Platforms/iOS/Entitlements.plist "$APP_PATH"
```

## Architecture Patterns

### App Intents: Swift ↔ C# Bridge

App Intents **must** be defined in Swift (compile-time metadata extraction). The pattern:

1. **Swift layer** — Thin declarations: `AppIntent`, `AppEntity`, `AppEnum`, `@objc` bridge protocol
2. **Binding library** — `ApiDefinition.cs` maps `@objc` types to C#; `<XcodeProject>` auto-builds xcframework
3. **MAUI app** — Implements the bridge protocol in C#, all business logic stays in .NET

### Widgets: File-Based IPC

Widgets run in a **separate process**. Communication uses JSON files in the App Group shared container — never UserDefaults (unreliable cross-process).

## Critical Conventions

### Swift Bridge Rules

- **Always** use `@objc(ClassName)` on Swift classes and protocols — prevents name mangling that breaks linking
- No nullable value types across the bridge — use sentinel values (`-1` for nil int, `""` for nil string)
- In `ApiDefinition.cs`, use `[NullAllowed]` instead of C# `?` nullable syntax

### Build System Rules

- `Metadata.appintents` must be in the app bundle — if intents don't appear, check `ls App.app/Metadata.appintents/`
- Always use `$(AppBundleDir)/` with explicit trailing `/` in MSBuild paths
- Entitlements plist files must use **LF line endings** (not CRLF)
- Widget bundle ID must be a child of the app bundle ID (e.g., `com.example.app.widgetextension`)

## Agent Skills

The `.agents/skills/` directory contains detailed implementation guides with code templates:

- **`maui-ios-appintents/SKILL.md`** — Complete workflow for adding App Intents to any MAUI app. Reference templates in `references/swift-patterns.md`, `references/csharp-binding.md`, `references/build-integration.md`.
- **`maui-ios-widget/SKILL.md`** — Complete workflow for adding widgets to any MAUI app. Reference templates in `references/swift-templates.md`, `references/csharp-templates.md`, `references/project-config.md`, `references/troubleshooting.md`.

When implementing App Intents or Widget features, **read the relevant SKILL.md first** — it contains the step-by-step workflow and the `references/` files contain complete code patterns to adapt.
