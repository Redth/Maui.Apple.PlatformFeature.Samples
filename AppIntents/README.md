# .NET MAUI + Siri App Intents Sample

A comprehensive sample demonstrating how to integrate **Apple Siri App Intents** into a **.NET MAUI** iOS app. This project serves as a canonical guide for MAUI developers who want to add Siri voice shortcuts, Spotlight integration, and Shortcuts app support to their apps.

## What This Sample Demonstrates

| Feature | Description |
|---------|-------------|
| **6 App Intents** | CreateTask, OpenTask, CompleteTask, ListTasks, SearchTasks, SetDueDate |
| **AppEntity** | `TaskEntity` with full entity query (by ID, string search, suggested) |
| **AppEnum** | `TaskPriority` (4 cases), `TaskCategory` (5 cases) |
| **Parameter Types** | String, Int, Bool, Date, AppEnum, AppEntity, optionals |
| **AppShortcutsProvider** | Multiple Siri phrases per intent |
| **PredictableIntent** | Prediction configurations for proactive system suggestions |
| **Intent Donation** | Donates intents from C# when users act in the MAUI UI |
| **Swift ↔ C# Bridge** | In-process communication via @objc protocol binding |
| **Dialog Responses** | Rich Siri dialog confirmations and results |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    .NET MAUI App (C#)                        │
│  ┌──────────┐  ┌──────────────┐  ┌───────────────────────┐  │
│  │  Views/   │  │  ViewModels/ │  │  Services/            │  │
│  │  XAML UI  │  │  MVVM        │  │  ITaskService         │  │
│  └──────────┘  └──────────────┘  │  TaskService           │  │
│                                  └───────────┬───────────┘  │
│                                              │              │
│  ┌───────────────────────────────────────────┴───────────┐  │
│  │  Platforms/iOS/AppIntentsBridge.cs                     │  │
│  │  Implements TaskDataProvider protocol in C#            │  │
│  └───────────────────────────────────────────┬───────────┘  │
├──────────────────────────────────────────────┼──────────────┤
│       .NET iOS Binding Library               │              │
│  ┌───────────────────────────────────────────┴───────────┐  │
│  │  ApiDefinition.cs                                     │  │
│  │  Maps Swift @objc types → C# interfaces               │  │
│  │  <XcodeProject> auto-builds xcframework from Xcode    │  │
│  └───────────────────────────────────────────┬───────────┘  │
├──────────────────────────────────────────────┼──────────────┤
│         Xcode Framework Project              │              │
│  ┌───────────────────────────────────────────┴───────────┐  │
│  │  Bridge/    → @objc TaskDataProvider protocol         │  │
│  │               + IntentDonationBridge (donate from C#) │  │
│  │  Enums/     → TaskPriority, TaskCategory (AppEnum)    │  │
│  │  Entities/  → TaskEntity (AppEntity) + TaskQuery      │  │
│  │  Intents/   → 6 AppIntent implementations             │  │
│  │  Shortcuts/ → AppShortcutsProvider with Siri phrases  │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

The binding project uses **[Native Library Interop](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop/)** (`<XcodeProject>` MSBuild item) to automatically build the Swift framework during `dotnet build` — no separate shell scripts or Makefile steps needed.

### Data Flow: Siri → Swift → C#

1. User says: **"Create a task in TaskTracker"**
2. iOS matches the phrase via `AppShortcutsProvider`
3. Siri runs `CreateTaskIntent.perform()` (Swift)
4. The intent calls `TaskBridgeManager.shared.provider.createTask(...)` 
5. The bridge calls the C# `AppIntentsBridgeProvider.CreateTask()` method
6. C# `TaskService` creates the task and returns it
7. Swift wraps the result as `TaskEntity` and returns dialog to Siri
8. Siri speaks: **"Created 'Buy groceries' with Medium priority in Shopping"**

### Intent Discovery: C# → Swift → System

When users perform actions in the MAUI UI, the app **donates** those intents to the system so iOS can learn patterns and proactively suggest shortcuts:

1. User creates a task in the MAUI UI
2. `TaskDetailViewModel` calls `IIntentDonationService.DonateCreateTask(task)`
3. The iOS implementation calls `IntentDonationBridge.Shared.DonateCreateTask(...)`
4. Swift creates a `CreateTaskIntent` with the task's parameters
5. `IntentDonationManager.shared.donate(intent:)` tells the system about this action
6. Over time, the system learns the user's patterns and suggests shortcuts in Spotlight, Siri Suggestions, etc.

The app also uses `PredictableIntent` on 4 intents (Create, Complete, List, Search) to provide prediction configurations that help the system proactively suggest actions.

## Prerequisites

- **macOS** (required for iOS development)
- **.NET 10 SDK** with MAUI workload (`dotnet workload install maui`)
- **Xcode 16+** with iOS 17+ SDK
- **iOS 17+ device or simulator** — App Intents register and execute on the simulator (tested on iPhone 17 / iOS 26.2). Full Siri voice interaction requires a physical device.
- Apple Developer account (for device deployment and Siri entitlements)

## Project Structure

```
MauiAppIntentsSample/
├── MauiAppIntentsSample.slnx              # Solution file
├── Makefile                                # Convenience wrapper (optional)
├── README.md                               # This file
│
└── src/
    ├── MauiAppIntentsSample/               # .NET MAUI App
    │   ├── Models/TaskItem.cs              # C# data model
    │   ├── Services/                       # Business logic
    │   │   ├── ITaskService.cs            # Task CRUD interface
    │   │   ├── TaskService.cs             # In-memory implementation
    │   │   └── IIntentDonationService.cs  # Intent donation interface
    │   ├── ViewModels/                     # MVVM view models
    │   ├── Views/                          # XAML pages
    │   ├── Converters/                     # Value converters
    │   └── Platforms/iOS/
    │       ├── AppDelegate.cs              # Wires up the bridge
    │       ├── AppIntentsBridge.cs          # C# TaskDataProvider impl
    │       ├── IntentDonationService.cs    # iOS intent donation impl
    │       └── Entitlements.plist          # Siri + App Group entitlements
    │
    ├── MauiAppIntentsSample.AppIntents/    # Xcode Framework Project
    │   ├── MauiAppIntentsSampleIntents.xcodeproj  # Xcode project (auto-built by MSBuild)
    │   └── Sources/
    │       ├── Bridge/                     # @objc bridge protocol + DTOs + donation bridge
    │       ├── Enums/                      # AppEnum types
    │       ├── Entities/                   # AppEntity + EntityQuery
    │       ├── Intents/                    # 6 AppIntent implementations (with PredictableIntent)
    │       └── Shortcuts/                  # AppShortcutsProvider
    │
    └── MauiAppIntentsSample.Binding/       # .NET iOS Binding Library
        ├── MauiAppIntentsSample.Binding.csproj  # <XcodeProject> builds xcframework
        ├── ApiDefinition.cs                # ObjC → C# type mapping
        └── StructsAndEnums.cs
```

## Building

The Swift framework is built **automatically** during `dotnet build` via the `<XcodeProject>` MSBuild integration (Native Library Interop). No separate build steps needed.

### Build
```bash
# Build everything (Swift framework + binding + MAUI app)
dotnet build src/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios
```

### Build for Simulator
```bash
# Builds without code signing (required for simulator)
dotnet build src/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=""

# Or use the Makefile shortcut:
make sim
```

### Clean
```bash
dotnet clean src/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios
```

### How the Build Works

1. `dotnet build` triggers the MAUI project, which depends on the binding project
2. The binding project's `<XcodeProject>` item triggers `xcodebuild archive` for both device and simulator
3. An xcframework is automatically created and linked as a `NativeReference`
4. A custom MSBuild target extracts `Metadata.appintents` from the xcarchive
5. The MAUI project copies `Metadata.appintents` into the app bundle

## Testing Siri Intents

### On Simulator
1. Build for simulator: `dotnet build src/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=""`
2. Install on a booted simulator:
   ```bash
   xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/MauiAppIntentsSample.app
   xcrun simctl launch booted com.companyname.mauiappintentssample
   ```
3. Open the **Shortcuts** app on the simulator
4. All 4 App Shortcuts (Create Task, Open Task, Complete Task, List Tasks) should appear under "TaskTracker"
5. Tap a shortcut to execute it — e.g. "List Tasks" returns task results via the bridge

> **Note:** App Intents register and execute fully on the simulator. Siri voice interaction is limited — use the Shortcuts app UI to test intent execution.

### On Device
1. Deploy the app to an iOS 17+ device
2. Open the app at least once (this registers the shortcuts)
3. Invoke Siri and say one of the registered phrases:
   - "Create a task in TaskTracker"
   - "Show my tasks in TaskTracker"
   - "Complete a task in TaskTracker"
   - "Search tasks in TaskTracker"
   - "Set due date in TaskTracker"

### In Shortcuts App
1. Open the **Shortcuts** app on device or simulator
2. Tap **+** to create a new shortcut
3. Search for "TaskTracker"
4. All 6 intents should appear as available actions

### Debugging Tips
- Use Xcode's **Console.app** to see `[AppIntents]` log messages
- Check that the bridge is wired: look for `[AppIntents] Bridge wired up successfully.` in logs
- If intents aren't appearing, verify `Metadata.appintents` is in the app bundle:
  ```bash
  ls -la path/to/MauiAppIntentsSample.app/Metadata.appintents/
  ```
- Siri may take a few minutes to index new shortcuts after first launch
- **Common build pitfall:** The MSBuild `$(AppBundleDir)` property may not end with a trailing `/`. The `CopyAppIntentsMetadata` target uses `$(AppBundleDir)/Metadata.appintents/` (with explicit `/`) — without it, metadata gets placed _outside_ the app bundle and intents silently fail to register

## Customizing for Your App

### Adding a New Intent

1. **Swift side** — Create a new file in `Sources/Intents/`:
   ```swift
   struct MyNewIntent: AppIntent {
       static var title: LocalizedStringResource = "My Action"
       
       @Parameter(title: "Name")
       var name: String
       
       func perform() async throws -> some IntentResult & ProvidesDialog {
           guard let provider = TaskBridgeManager.shared.provider else {
               throw IntentError.appNotReady
           }
           // Call your bridge method
           return .result(dialog: "Done!")
       }
   }
   ```

2. **Add to shortcuts** — Update `TaskTrackerShortcuts.swift`:
   ```swift
   AppShortcut(
       intent: MyNewIntent(),
       phrases: ["Do my action in \(.applicationName)"],
       shortTitle: "My Action",
       systemImageName: "star"
   )
   ```

3. **Bridge protocol** — Add the method to `TaskDataProvider` in Swift and `ApiDefinition.cs` in C#

4. **C# implementation** — Implement the new method in `AppIntentsBridgeProvider.cs`

5. **Rebuild** — Run `dotnet build -f net10.0-ios`

### Adding a New AppEnum

```swift
// Sources/Enums/MyEnum.swift
enum MyEnum: String, AppEnum {
    case optionA, optionB, optionC
    
    static var typeDisplayRepresentation: TypeDisplayRepresentation = "My Enum"
    static var caseDisplayRepresentations: [MyEnum: DisplayRepresentation] = [
        .optionA: "Option A",
        .optionB: "Option B",
        .optionC: "Option C"
    ]
}
```

### Adding a New AppEntity

See `Sources/Entities/TaskEntity.swift` and `Sources/Entities/TaskQuery.swift` for the pattern. Key requirements:
- Conform to `AppEntity`
- Provide a `defaultQuery` (usually `EntityStringQuery` for Siri search)
- Include `displayRepresentation` for how Siri shows the entity

## Why Swift is Required

App Intents use **compile-time metadata extraction** (`appintentsmetadataprocessor`) that is deeply integrated into the Swift compiler and Xcode build system. This metadata is what allows iOS to discover your intents without running your code. There is no way to generate this metadata from C# — the intent definitions must be in Swift.

However, this sample shows that you can keep **all business logic in C#** and only use Swift as a thin declaration layer for the intent definitions themselves.

## Future: Slim Binding Library Concept

A reusable NuGet package could simplify this pattern:
- **C# Source Generator** — Define intents with C# attributes, auto-generate Swift code
- **MSBuild SDK** — Automate xcframework build and metadata injection during `dotnet build`
- **Pre-built bridge** — Generic JSON-based bridge eliminating per-project protocol definitions

This would allow developers to write something like:
```csharp
[AppIntent("Create Task")]
[AppShortcutPhrase("Create a task in {applicationName}")]
public class CreateTaskIntent : IAppIntent<TaskEntity>
{
    [IntentParameter("Title")]
    public string Title { get; set; }
}
```

## References

- [Apple: App Intents Framework](https://developer.apple.com/documentation/appintents/)
- [Apple: Integrating with Siri and Apple Intelligence](https://developer.apple.com/documentation/appintents/integrating-actions-with-siri-and-apple-intelligence)
- [Microsoft: Native Library Interop for .NET MAUI](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop/)
- [CommunityToolkit/Maui.NativeLibraryInterop](https://github.com/CommunityToolkit/Maui.NativeLibraryInterop)
- [Apple: Adding Parameters to an App Intent](https://developer.apple.com/documentation/appintents/adding-parameters-to-an-app-intent)

## License

MIT
