---
name: maui-ios-appintents
description: >
  Add Apple Siri App Intents to .NET MAUI iOS apps — the complete architecture, Swift framework,
  binding library, and C# bridge pattern. Use this skill whenever someone wants to integrate Siri,
  Shortcuts, or App Intents with a .NET MAUI app, add voice commands to a MAUI iOS app, create
  AppIntent/AppEntity/AppEnum types for a MAUI project, build a Swift xcframework for native
  interop with MAUI, or set up the @objc bridge between Swift and C# for App Intents. Also use
  when someone asks about making their MAUI app work with Siri, Apple Intelligence, Spotlight
  integration, or the iOS Shortcuts app. Even if the user just mentions "Siri" or "voice shortcuts"
  in the context of a .NET MAUI or Xamarin iOS app, this skill applies.
---

# .NET MAUI + iOS App Intents Integration

This skill guides the implementation of Apple App Intents (Siri, Shortcuts, Spotlight) in .NET MAUI iOS apps. It covers the complete architecture from Swift intent definitions through the @objc bridge to C# business logic.

## Table of Contents
1. [Why This Architecture](#why-this-architecture)
2. [Solution Structure](#solution-structure)
3. [Implementation Workflow](#implementation-workflow)
4. [Critical Gotchas](#critical-gotchas)
5. [Reference Files](#reference-files)
6. [Completion Checklist](#completion-checklist)

---

## Why This Architecture

App Intents are **Swift-only** — no way around this. Three things make it impossible to define them in C#:

1. **Compile-time metadata extraction**: Xcode's `appintentsmetadataprocessor` scans compiled Swift binaries for types conforming to `AppIntent`, `AppEntity`, `AppEnum` and generates a `Metadata.appintents` directory. iOS reads this at install time to discover what intents the app offers. Without it, Siri will never see the intents.

2. **Swift-specific constructs**: `@Parameter` property wrappers, `@Property` annotations, `@AppShortcutsBuilder` result builders, and protocol conformance (`AppIntent`, `AppEntity`, `AppEnum`, `AppShortcutsProvider`) have no C# equivalents.

3. **Protocol requirements**: These protocols require specific static properties (`title`, `typeDisplayRepresentation`, `caseDisplayRepresentations`) and methods (`perform()`, `entities(for:)`) that must be compiled by the Swift compiler.

**The good news**: Only the *intent declarations* need to be Swift. All business logic, data storage, UI, and app architecture stay in C#. The Swift layer is a thin declaration shell that delegates to C# via an @objc bridge.

The architecture uses three projects:

```
┌─────────────────────────────────┐
│  .NET MAUI App (C#)             │  Business logic, UI, data, services
│  └─ Platforms/iOS/              │  Bridge implementation (C# → Swift)
│     └─ AppIntentsBridge.cs      │  Implements Swift protocol in C#
├─────────────────────────────────┤
│  .NET iOS Binding Library       │  ApiDefinition.cs maps @objc types → C#
│  └─ <XcodeProject> item        │  Auto-builds xcframework from Xcode project
├─────────────────────────────────┤
│  Xcode Framework Project        │  App Intents definitions + @objc bridge
│  └─ Sources/                    │  AppIntent, AppEntity, AppEnum, etc.
└─────────────────────────────────┘
```

The binding project uses **[Native Library Interop](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop/)** (`<XcodeProject>` MSBuild item) from the .NET iOS SDK. This means `dotnet build` automatically: builds the Xcode project → creates xcarchive → creates xcframework → links it as a NativeReference. No shell scripts or Makefile needed.

**Data flow when Siri invokes an intent:**

1. User speaks → iOS matches phrase from `AppShortcutsProvider`
2. iOS calls Swift `perform()` on the matched `AppIntent`
3. Swift calls `TaskBridgeManager.shared.provider.someMethod(...)` — an @objc protocol
4. The C# binding routes this to the C# class implementing the protocol
5. C# executes business logic, returns result
6. Swift wraps result as `IntentResult` with dialog → Siri speaks response

This is **in-process** communication — the framework is loaded into the MAUI app's process. No IPC, no serialization overhead, no App Group needed for basic data access.

---

## Solution Structure

For a project called `{AppName}`, create this layout:

```
{AppName}/
├── {AppName}.slnx
├── src/
│   ├── {AppName}/                          # .NET MAUI App
│   │   ├── {AppName}.csproj
│   │   ├── Models/                         # C# data models
│   │   ├── Services/                       # Business logic interfaces + implementations
│   │   ├── Platforms/iOS/
│   │   │   ├── AppDelegate.cs              # Wire up bridge in FinishedLaunching
│   │   │   ├── AppIntentsBridge.cs         # C# class implementing Swift protocol
│   │   │   └── Entitlements.plist          # Siri + App Group entitlements
│   │   └── ...
│   │
│   ├── {AppName}.AppIntents/               # Xcode Framework Project
│   │   ├── {FrameworkName}.xcodeproj       # Xcode project (auto-built by MSBuild)
│   │   └── Sources/
│   │       ├── Bridge/
│   │       │   ├── BridgeModels.swift       # @objc DTO classes
│   │       │   └── DataBridge.swift         # @objc protocol + manager singleton
│   │       ├── Enums/                       # AppEnum types
│   │       ├── Entities/                    # AppEntity + EntityQuery types
│   │       ├── Intents/                     # AppIntent implementations
│   │       │   └── IntentError.swift        # Error enum
│   │       └── Shortcuts/
│   │           └── AppShortcuts.swift        # AppShortcutsProvider
│   │
│   └── {AppName}.Binding/                  # .NET iOS Binding Library
│       ├── {AppName}.Binding.csproj        # <XcodeProject> auto-builds xcframework
│       ├── ApiDefinition.cs                # ObjC → C# type mapping
│       └── StructsAndEnums.cs
```

---

## Implementation Workflow

Work through these steps in order. Read the referenced files for code patterns.

### Step 1: Design the Data Model and Intents

Before writing any code, decide:
- **What C# data types** will be exposed as AppEntities? (e.g., a `TaskItem`, `Recipe`, `Contact`)
- **What enums** will be exposed as AppEnums? (e.g., Priority, Category, Status)
- **What actions** will users perform via Siri? Each becomes an AppIntent.
- **What parameter types** does each intent need? Supported: `String`, `Int`, `Double`, `Bool`, `Date`, `AppEnum`, `AppEntity`, and optional variants of all.

**Intent coverage**: Even if the user mentions only 1-2 actions, consider the standard set of intents that most apps benefit from. Create at least 3 intents from this menu:
- **Create** — Add a new item (most common starting point)
- **Open** — Navigate to an item in the app (`openAppWhenRun = true`)
- **Complete/Toggle** — Change a boolean state (e.g., mark done)
- **List/Filter** — Show filtered results with optional enum/bool filters
- **Search** — Find items by text query
- **Update Field** — Change a specific field (e.g., set due date)

Suggest intents that make sense for the domain, even if the user didn't explicitly list them.

### Step 2: Build the C# App Foundation

Create the MAUI project with models, services, and UI. The service layer should have an interface (e.g., `ITaskService`) with CRUD + query methods — this is what the bridge will call. Register it as a singleton in DI.

If this is an existing app, identify the service interface to expose and ensure it's registered as a singleton.

### Step 3: Create the Xcode Framework Project

Read `references/swift-patterns.md` for complete code patterns.

Create an Xcode Framework project (`.xcodeproj`) targeting iOS 17+. Set these build settings in the Xcode project:
- `SWIFT_REFLECTION_METADATA_LEVEL = all` (critical for App Intents metadata)
- `SWIFT_INSTALL_OBJC_HEADER = YES`
- Link the `AppIntents` framework

Then implement in this order:

1. **Bridge layer** (`Sources/Bridge/`) — `@objc` DTO class + protocol + manager singleton. This is the API surface between Swift and C#. Critical rules:
   - Use `@objc(ClassName)` syntax to set explicit ObjC class names (prevents Swift name mangling)
   - Use `Int` raw values for enums (not Swift enums directly)
   - Use sentinel values for optional value types (`-1` for "nil int", empty string for "nil string")
   - Only `NSObject` subclasses and `@objc` protocols cross the bridge

2. **AppEnums** (`Sources/Enums/`) — Each Swift `AppEnum` maps to a C# enum via raw `Int` values.

3. **AppEntity + EntityQuery** (`Sources/Entities/`) — The entity wraps bridge DTOs. The query calls the bridge protocol to fetch data.

4. **AppIntents** (`Sources/Intents/`) — Each intent's `perform()` calls `BridgeManager.shared.provider.someMethod(...)`. Include an `IntentError` enum. For intents with predictable parameter patterns, also conform to `PredictableIntent` and provide a `predictionConfiguration`.

5. **AppShortcutsProvider** (`Sources/Shortcuts/`) — Define Siri phrases. Always include `\(.applicationName)` in phrases.

6. **IntentDonationBridge** (`Sources/Bridge/`) — `@objc` class with a `shared` singleton that wraps `IntentDonationManager.shared.donate(intent:)`. For each intent type, provide a method (e.g., `donateCreateTask(title:...)`) that creates the intent, populates parameters, and donates it. Also provide `deleteTaskDonations(taskId:)` using `IntentDonationManager.shared.deleteDonations(matching:)`. This is how C# tells the system about user actions performed in the MAUI UI.

### Step 4: Create the .NET iOS Binding Library with `<XcodeProject>`

Read `references/csharp-binding.md` for complete ApiDefinition.cs patterns.

1. Create project: `dotnet new iosbinding`
2. Replace any `<NativeReference>` with an `<XcodeProject>` item pointing to the `.xcodeproj`:
   ```xml
   <XcodeProject Include="../{AppName}.AppIntents/{FrameworkName}.xcodeproj">
     <SchemeName>{FrameworkName}</SchemeName>
     <ForceLoad>true</ForceLoad>
     <SmartLink>false</SmartLink>
   </XcodeProject>
   ```
   This automatically builds the xcframework during `dotnet build` — no shell scripts needed.
3. Add a custom MSBuild target `ExtractAppIntentsMetadata` that finds and copies `Metadata.appintents` from the xcarchive output (see `references/build-integration.md`)
4. Write `ApiDefinition.cs` mapping every @objc type from the bridge layer — including the `IntentDonationBridge` singleton
5. The `[Protocol, Model]` on the bridge protocol auto-generates `I{ProtocolName}` interface

### Step 5: Wire the Bridge in the MAUI App

Read `references/csharp-binding.md` (bridge implementation section).

1. Create `AppIntentsBridgeProvider.cs` in `Platforms/iOS/` — a C# class that inherits the generated protocol Model class and delegates to your service
2. Create `IIntentDonationService` interface and `IntentDonationService` iOS implementation that wraps the `IntentDonationBridge` binding. Call donation methods from ViewModels when users create/complete/open tasks.
3. In `AppDelegate.FinishedLaunching`, get the service from DI, create the bridge provider, set it on the manager singleton
4. Add MSBuild target `CopyAppIntentsMetadata` to copy `Metadata.appintents` from the binding project's intermediate output into the app bundle
5. Add `Entitlements.plist` with Siri entitlement (and optionally App Group)

### Step 6: Build and Test

1. Build everything with a single command: `dotnet build -f net10.0-ios`
   (This builds the Xcode project → xcframework → binding → MAUI app automatically)
2. **Simulator testing** (fastest verification loop):
   - Build without code signing: `dotnet build -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=""`
   - Install: `xcrun simctl install booted /path/to/{AppName}.app`
   - Launch: `xcrun simctl launch booted {bundle-id}`
   - Open Shortcuts app → verify intents appear under your app name
   - Tap a shortcut to confirm the Swift→C# bridge executes correctly
   - Check console for `[AppIntents] Bridge wired up successfully.`
4. **Device testing** (for Siri voice):
   - Deploy to iOS 17+ device with Siri entitlement in provisioning profile
   - Open app once (registers shortcuts)
   - Test with Siri or Shortcuts app

---

## Critical Gotchas

These are real issues discovered during implementation — not theoretical concerns:

### 1. `@objc(ClassName)` is REQUIRED
Without explicit `@objc(ClassName)`, Swift mangles class names. Instead of `BridgeTaskItem`, the linker sees `_TtC27MauiAppIntentsSampleIntents14BridgeTaskItem`. The .NET binding expects the clean name → undefined symbol errors at link time.

**Always write:**
```swift
@objc(BridgeTaskItem) public class BridgeTaskItem: NSObject { ... }
@objc(MyBridgeManager) public class MyBridgeManager: NSObject { ... }
@objc(MyDataProvider) public protocol MyDataProvider: AnyObject { ... }
```

### 2. No Nullable Value Types Across the Bridge
ObjC doesn't have optional `Int?` or `Bool?`. Use sentinel values:
- `Int` → use `-1` for nil
- `String` → use empty string `""` for nil
- `Date?` is fine (it's a reference type in ObjC)
- `Bool` → can't be optional, use a separate flag or always provide a value

### 3. Binding Project Nullable Syntax
In `ApiDefinition.cs`, don't use C# nullable reference type annotations (`?`). Use `[NullAllowed]` attribute instead. The binding code generator doesn't support `#nullable` context.

### 4. Metadata Must Be in the App Bundle
The `Metadata.appintents` directory must end up in the final `.app` bundle or iOS won't discover the intents. The MSBuild `CopyAppIntentsMetadata` target handles this. If intents don't appear in Siri/Shortcuts, check the app bundle for this directory first.

### 5. Xcode Project Build Settings
The Xcode project must have `SWIFT_REFLECTION_METADATA_LEVEL = all` in its build settings. Without this, `appintentsmetadataprocessor` can't find AppIntent types and `Metadata.appintents` will be empty. The `<XcodeProject>` MSBuild integration already passes `BUILD_LIBRARY_FOR_DISTRIBUTION=YES` automatically.

### 6. Build Order is Automatic
With `<XcodeProject>`, the build order is handled by MSBuild: Xcode project → xcframework → binding → MAUI app. Just run `dotnet build` and everything builds in the correct order.

### 7. Simulator Testing Works (but Siri Voice is Limited)
App Intents **do register and execute** on the iOS Simulator. You can verify intent registration in the Shortcuts app, tap shortcuts to run them, and confirm the full Swift→@objc→C# bridge pipeline works. However, Siri voice interaction has limited functionality on the simulator — use a physical iOS 17+ device for voice testing.

### 8. `$(AppBundleDir)` Path Separator
The MSBuild property `$(AppBundleDir)` may or may not end with a trailing `/`. Always write `$(AppBundleDir)/Metadata.appintents` (with explicit `/`), never `$(AppBundleDir)Metadata.appintents`. Without the separator, the copy destination becomes `MyApp.appMetadata.appintents` — outside the bundle — and iOS won't find the intents.

### 9. Intent Donation is Fire-and-Forget
Intent donation (`IntentDonationManager.shared.donate`) is async and can fail silently. Wrap donation calls in try/catch and log failures, but never let them crash the app. The donation is a hint to the system, not a critical operation. Also: **only donate when the user acts in your app's UI** — Siri/Shortcuts donations are handled automatically by the system.

---

## Reference Files

Read these for detailed code patterns and examples:

| File | When to Read | Contents |
|------|-------------|----------|
| `references/swift-patterns.md` | When writing Swift code | Complete patterns for AppEnum, AppEntity, EntityQuery, AppIntent, AppShortcutsProvider, bridge DTOs, bridge protocol, PredictableIntent, IntentDonationBridge, error enum |
| `references/csharp-binding.md` | When writing C# binding + bridge | ApiDefinition.cs, binding csproj, bridge implementation in C#, IntentDonationBridge binding, IIntentDonationService, ViewModel integration, AppDelegate wiring |
| `references/build-integration.md` | When setting up build pipeline | Xcode project setup, `<XcodeProject>` MSBuild item, metadata extraction, Makefile, troubleshooting |

---

## Completion Checklist

A successful implementation has ALL of these:

- [ ] Xcode project compiles for both iOS device and simulator
- [ ] `Metadata.appintents` directory exists in xcarchive output (contains `extract.actionsdata` and `version.json`)
- [ ] `<XcodeProject>` item in binding .csproj builds xcframework automatically via `dotnet build`
- [ ] `ExtractAppIntentsMetadata` target copies metadata from xcarchive to intermediate output
- [ ] Binding project compiles (`dotnet build` succeeds)
- [ ] MAUI app compiles end-to-end with single `dotnet build -f net{version}-ios` (no prior steps needed)
- [ ] No "Undefined symbols" linker errors (confirms `@objc(ClassName)` is correct)
- [ ] Framework appears in `{app}.app/Frameworks/` directory
- [ ] `Metadata.appintents` is copied into the app bundle (via `CopyAppIntentsMetadata` target) — verify with `ls {app}.app/Metadata.appintents/`
- [ ] Bridge is wired in `AppDelegate.FinishedLaunching` (log message confirms it)
- [ ] Entitlements.plist includes `com.apple.developer.siri = true`
- [ ] On simulator: intents appear in Shortcuts app under the app name
- [ ] On simulator: tapping a shortcut executes successfully through the bridge
- [ ] On device: Siri responds to registered phrases
- [ ] `PredictableIntent` conformance on key intents (Create, Complete, List, Search)
- [ ] `IntentDonationBridge` exposes donation methods to C# via binding
- [ ] `IIntentDonationService` + iOS implementation wired in DI
- [ ] ViewModels call donation service after user actions (create, complete, open)
