# Maui.Apple.PlatformFeature.Samples

Samples and AI agent skills for integrating Apple platform features into .NET MAUI iOS apps. Each sample is a self-contained project demonstrating a specific integration pattern.

## Samples

### [App Intents](AppIntents/) — Siri, Shortcuts & Spotlight

A comprehensive sample integrating **Apple Siri App Intents** into a .NET MAUI iOS app using a three-project architecture:

| Component | Description |
|-----------|-------------|
| **Xcode Framework** (`MauiAppIntentsSample.AppIntents`) | Swift `AppIntent`, `AppEntity`, `AppEnum` definitions + `@objc` bridge protocol |
| **Binding Library** (`MauiAppIntentsSample.Binding`) | Maps `@objc` types to C# via `ApiDefinition.cs`; auto-builds xcframework with `<XcodeProject>` |
| **MAUI App** (`MauiAppIntentsSample`) | Implements the bridge protocol in C#, keeping all business logic in .NET |

App Intents require Swift because iOS uses compile-time metadata extraction to discover intents — this can't be done from C#. This sample demonstrates how to keep Swift as a thin declaration layer while all business logic stays in C#. Includes 6 intents, entity queries, enum types, intent donation, predictable intents, and `AppShortcutsProvider` phrases.

```bash
# Build everything (Swift framework builds automatically)
dotnet build AppIntents/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios

# Build for simulator
cd AppIntents && make sim
```

See the [App Intents README](AppIntents/README.md) for full architecture details, build instructions, and customization guide.

### [Widgets](Widgets/) — iOS Home Screen Widgets

A .NET MAUI app with a bundled **iOS Widget Extension**, demonstrating robust bidirectional communication between the app and the widget:

| Direction | Mechanism |
|-----------|-----------|
| **App → Widget** | JSON file in App Group container + WidgetKit reload |
| **Widget → App (tap)** | Deep link via `widgetURL()` |
| **Widget → App (interactive)** | AppIntents buttons + file I/O |

The widget is a Swift/SwiftUI extension (`XCodeWidget/`) that communicates with the MAUI app through JSON files in the App Group shared container. The MSBuild integration automatically builds the widget extension during `dotnet build`.

```bash
# Build for simulator (widget builds automatically)
dotnet build Widgets/MauiAppleWidgets.csproj -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignRequireProvisioningProfile=false
```

See the [Widgets README](Widgets/README.md) for full architecture details, build instructions, and known gotchas.

## AI Agent Skills

The `.agents/skills/` directory contains **Copilot agent skills** — structured guides with code templates that teach AI coding agents how to add these Apple features to any .NET MAUI app:

| Skill | Description |
|-------|-------------|
| [`maui-ios-appintents`](.agents/skills/maui-ios-appintents/SKILL.md) | Step-by-step guide for adding Siri/Shortcuts/App Intents to a MAUI app. Includes Swift patterns, C# binding templates, and build integration references. |
| [`maui-ios-widget`](.agents/skills/maui-ios-widget/SKILL.md) | Step-by-step guide for adding iOS home screen widgets to a MAUI app. Includes Swift widget templates, C# service layer patterns, and project configuration. |

Each skill contains a `SKILL.md` with the implementation workflow and a `references/` directory with complete code templates. The samples in this repo serve as the canonical reference implementations for these skills.

## Prerequisites

- **macOS** (required for iOS development)
- **.NET 10 SDK** with MAUI workload (`dotnet workload install maui`)
- **Xcode 16+** with iOS 17+ SDK
- **iOS 17+ device or simulator**
- [xcodegen](https://github.com/yonaskolb/XcodeGen) (optional — for Widgets sample Xcode project generation)

## License

MIT
