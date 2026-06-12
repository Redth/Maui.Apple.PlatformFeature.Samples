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
| **Generated C# Path** | Prototype `Maui.AppIntents` package generates SwiftPM AppIntent/AppEnum/AppEntity declarations from C# |

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
│         Swift App Intents Framework          │              │
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
├── MauiAppIntentsSample/                   # .NET MAUI App
│   ├── Models/TaskItem.cs                  # C# data model
│   ├── Services/                           # Business logic
│   │   ├── ITaskService.cs                 # Task CRUD interface
│   │   ├── TaskService.cs                  # In-memory implementation
│   │   └── IIntentDonationService.cs       # Intent donation interface
│   ├── ViewModels/                         # MVVM view models
│   ├── Views/                              # XAML pages
│   ├── Converters/                         # Value converters
│   └── Platforms/iOS/
│       ├── AppDelegate.cs                  # Wires up the bridge
│       ├── AppIntentsBridge.cs             # C# TaskDataProvider impl
│       ├── IntentDonationService.cs        # iOS intent donation impl
│       └── Entitlements.plist              # Siri + App Group entitlements
│
├── MauiAppIntentsSample.AppIntents/        # Swift App Intents framework
│   ├── MauiAppIntentsSampleIntents.xcodeproj  # Current default Xcode project
│   ├── Package.swift                       # Experimental SwiftPM/no-.xcodeproj entry point
│   └── Sources/
│       ├── Bridge/                         # @objc bridge protocol + DTOs + donation bridge
│       ├── Enums/                          # AppEnum types
│       ├── Entities/                       # AppEntity + EntityQuery
│       ├── Intents/                        # 6 AppIntent implementations (with PredictableIntent)
│       └── Shortcuts/                      # AppShortcutsProvider
│
├── MauiAppIntentsSample.Binding/           # .NET iOS Binding Library
│   ├── MauiAppIntentsSample.Binding.csproj # <XcodeProject> builds xcframework
│   ├── ApiDefinition.cs                    # ObjC → C# type mapping
│   └── StructsAndEnums.cs
│
├── Maui.AppIntents/                         # Prototype reusable C# authoring/MSBuild package
│   ├── AppIntentAttribute.cs                # C# attributes for intent declarations
│   ├── AppIntentResponse.cs                 # Handler contract + response envelope
│   ├── MauiAppIntentRegistry.cs             # JSON dispatcher registration/runtime
│   └── buildTransitive/                     # MSBuild SwiftPM generation + native packaging
├── Maui.AppIntents.Generator/                # Roslyn source generator for semantic discovery/managed glue
├── Maui.AppIntents.BuildTasks/               # Compiled MSBuild tasks for SwiftPM generation/validation
│
└── scripts/
    └── build-appintents-swiftpm.sh         # Experimental no-.xcodeproj Swift build spike
```

## Building

The Swift framework is built **automatically** during `dotnet build` via the `<XcodeProject>` MSBuild integration (Native Library Interop). No separate build steps needed.

### Build
```bash
# Build everything (Swift framework + binding + MAUI app)
dotnet build MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios
```

### Build for Simulator
```bash
# Builds without code signing (required for simulator)
dotnet build MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=""

# Or use the Makefile shortcut:
make sim
```

### Clean
```bash
dotnet clean MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios
```

### Experimental: Build App Intents with SwiftPM

The Swift App Intents target also includes a `Package.swift` so the same Swift declarations can be archived without using the checked-in `.xcodeproj`. This is a spike toward a generated App Intents pipeline where MAUI developers author C# models/attributes and the build emits Swift as an implementation detail.

```bash
# From the AppIntents directory
make swiftpm-spike
```

The script archives device and simulator frameworks, creates an xcframework, and copies the generated metadata to:

```text
artifacts/swiftpm-appintents/xcframeworks/MauiAppIntentsSampleIntents.xcframework
artifacts/swiftpm-appintents/Metadata.appintents
```

This target is **not** wired into the default MAUI build yet. The default build still uses `<XcodeProject>` while the no-XcodeProject path is validated.

### Prototype: C#-Authored App Intents Package

`Maui.AppIntents/` is the reusable package prototype for the generated path: app developers write C# intent handlers and set `MauiAppIntentsEnabled=true`; a Roslyn incremental generator semantically discovers the attributed symbols, emits managed registration/native bridge glue, and stores an intermediate manifest in assembly metadata. A compiled MSBuild task reads that manifest after `CoreCompile`, generates Swift declarations and a SwiftPM package under `obj/`, archives an xcframework, and copies `Metadata.appintents` without a checked-in `.xcodeproj`.

The current package vertical slice supports primitive parameters, optional values, `AppEnum` parameters, `AppEntity` parameters with dynamic query handlers, multi-select entity parameters, typed result values, entity properties, generated donations, and app shortcuts. It also generates the declarative Tier-A features from the Apple-docs audit: `supportedModes`/`IntentModes`, rich/conditional `ParameterSummary`, typed `AppIntentError` categories, `IndexedEntity` Spotlight indexing, `URLRepresentable*` deep links, and `UniqueAppEntity` singletons. The first interaction-flow feature is also generated: pre-execution `requestConfirmation` via `[AppIntent(RequiresConfirmation = true, …)]` (see `CompleteGeneratedTaskIntent`).

```csharp
[AppIntent("CreateTaskIntent", Title = "Create Task")]
[AppShortcut("Create a task in ${applicationName}", ShortTitle = "Create Task")]
public sealed class CreateTaskIntent : IAppIntentHandler<CreateTaskIntent.Request>
{
    public sealed record Request(
        [property: IntentParameter("Title")] string Title,
        [property: IntentParameter("Estimated Minutes", IsOptional = true)] int? EstimatedMinutes,
        [property: IntentParameter("Priority", IsOptional = true)] TaskPriorityLevel? Priority);

    public Task<AppIntentResponse> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        return Task.FromResult(AppIntentResponse.Succeeded($"Created {request.Title}"));
    }
}

[AppEnum("Task Priority")]
public enum TaskPriorityLevel
{
    [AppEnumCase("Low")]
    Low = 0,

    [AppEnumCase("Medium")]
    Medium = 1
}
```

Handlers can also return values to Shortcuts by implementing `IAppIntentHandler<TRequest, TResult>`:

```csharp
[AppIntent("CreateTaskIntent", Title = "Create Task")]
public sealed class CreateTaskIntent
    : IAppIntentHandler<CreateTaskIntent.Request, AppEntityReference<TaskItem>>
{
    public sealed record Request([property: IntentParameter("Title")] string Title);

    public Task<AppIntentResponse<AppEntityReference<TaskItem>>> HandleAsync(
        Request request,
        CancellationToken cancellationToken)
    {
        var task = CreateTask(request.Title);
        return Task.FromResult(AppIntentResponse<AppEntityReference<TaskItem>>.Succeeded(
            new AppEntityReference<TaskItem> { Id = task.Id, Display = task.Title, Subtitle = task.Notes },
            $"Created {task.Title}"));
    }
}
```

Entities are normal C# models plus a DI query handler:

```csharp
[AppEntity("TaskItem", TypeDisplayName = "Task")]
public sealed class TaskItem
{
    [AppEntityIdentifier]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [AppEntityDisplay]
    public string Title { get; set; } = "";

    [AppEntitySubtitle]
    public string? Notes { get; set; }

    [AppEntityProperty("Priority")]
    public TaskPriorityLevel Priority { get; set; }
}

[AppEntityQueryHandler(typeof(TaskItem))]
public sealed class TaskItemQueryHandler : IAppEntityQueryHandler<TaskItem>
{
    public Task<IReadOnlyList<TaskItem>> GetEntitiesAsync(IReadOnlyList<string> ids, CancellationToken token)
        => Task.FromResult<IReadOnlyList<TaskItem>>(ids.Select(LoadTaskById).OfType<TaskItem>().ToList());

    public Task<IReadOnlyList<TaskItem>> SearchEntitiesAsync(string query, CancellationToken token)
        => Task.FromResult(SearchTasks(query));

    public Task<IReadOnlyList<TaskItem>> SuggestedEntitiesAsync(CancellationToken token)
        => Task.FromResult(GetRecentIncompleteTasks());
}
```

Entity intent parameters use `AppEntityReference<TEntity>` so handlers re-fetch authoritative app data by ID. Multi-select entity parameters use `IReadOnlyList<AppEntityReference<TEntity>>` and generate Swift array parameters.

See `Maui.AppIntents/README.md` for the full package walkthrough, AppDelegate bridge hookup, build properties, entity setup, and current v1 scope.

The sample app currently wires generated create, complete, complete-many, and list intents alongside the hand-written Swift/binding sample to validate the end-to-end packaging and bridge model. During generated-path validation, the app-level `Metadata.appintents` copy comes from `Maui.AppIntents`, so the generated metadata is the bundle metadata iOS indexes.

Simulator validation has confirmed:

- the generated SwiftPM framework is embedded under `Frameworks/`
- `Metadata.appintents/extract.actionsdata` contains `CreateGeneratedTaskIntent`, `CompleteGeneratedTaskIntent`, `CompleteGeneratedTasksIntent`, and `ListGeneratedTasksIntent`
- generated `Task Priority` and `Task Category` enum metadata is extracted from C# `[AppEnum]` declarations
- generated `TaskItemEntity` and `TaskItemEntityQuery` metadata is extracted from C# `[AppEntity]` and `[AppEntityQueryHandler]` declarations
- generated entity property metadata is extracted from `[AppEntityProperty]` declarations
- generated `supportedModes`, `IndexedEntity`, `URLRepresentableEntity`/`URLRepresentableEnum`, conditional `ParameterSummary`, `UniqueAppEntity` (`TaskTrackerSettingsEntity`/`ShowTaskTrackerSettingsIntent`), and gated `requestConfirmation` (`CompleteGeneratedTaskIntent`) constructs typecheck and extract under the real Xcode toolchain
- typed `ReturnsValue<T>` metadata is extracted for value-returning intents
- generated donation wrappers can donate C#-authored intents through `MauiAppIntentsNative.Donate`
- the generated dispatcher and donation C ABI bridge symbols are exported from the framework
- the generated bundle validation target passes before codesigning
- startup logs include `[AppIntents] Generated bridge wired up successfully.`
- iOS indexes the generated shortcut phrase as `Create a generated task in TaskTracker`
- manual simulator execution works: tapping the generated "Create Generated Task" tile in Shortcuts, entering `Test`, invokes the generated SwiftPM `AppIntent`, reaches the C# handler (`[AppIntents] Generated handler invoked: Test`), and creates the `Test` task in the MAUI app
- `shortcuts://run-shortcut?...` and `/usr/bin/shortcuts run ...` look for user-authored shortcut files in this simulator; they do not directly invoke App Shortcuts from `AppShortcutsProvider`

### How the Build Works

1. The handwritten sample still builds its Swift framework through the binding project's `<XcodeProject>` item.
2. When `MauiAppIntentsEnabled=true`, the reusable package adds a Roslyn generator. The generator collects attributed C# symbols without regex parsing and emits managed glue plus a manifest attribute into the app intermediate assembly.
3. A compiled MSBuild task reads that manifest and generates a SwiftPM package under `obj/`.
4. The generated SwiftPM package is archived with `xcodebuild`, producing an xcframework and `Metadata.appintents` without a checked-in generated `.xcodeproj`.
5. MSBuild registers the generated xcframework as a `NativeReference` before `_ExpandNativeReferences`, copies generated `Metadata.appintents` into the `.app`, and validates the bundle before codesigning.
6. Incremental inputs/outputs track the app intermediate assembly, generated Swift, runtime shim, build script, metadata, xcframework, and validation stamp so xcodebuild is skipped when inputs are unchanged.

## Testing Siri Intents

### On Simulator
1. Build for simulator: `dotnet build MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=""`
2. Install on a booted simulator:
   ```bash
   xcrun simctl install booted MauiAppIntentsSample/bin/Debug/net10.0-ios/iossimulator-arm64/MauiAppIntentsSample.app
   xcrun simctl launch booted com.companyname.mauiappintentssample
   ```
3. Open the **Shortcuts** app on the simulator
4. All 6 App Shortcuts should appear under "TaskTracker"
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
