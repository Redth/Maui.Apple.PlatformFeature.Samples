# Maui.AppIntents

`Maui.AppIntents` is a prototype C# authoring package for Apple App Intents in .NET MAUI iOS apps. App developers write C# attributes, handlers, enums, and entity query handlers; the build generates the Swift declarations that iOS requires for Shortcuts, Siri, and Spotlight discovery.

The goal is **no user-written Swift and no checked-in Xcode project** for the generated path. Swift and Xcode are still used internally because Apple discovers App Intents from compile-time Swift metadata.

## What the package generates

1. A Roslyn incremental source generator discovers C# symbols such as `[AppIntent]`, `[AppEnum]`, and `[AppEntity]` during `CoreCompile`.
2. The generator emits managed registration, native bridge glue, and a JSON manifest embedded in the app intermediate assembly.
3. A compiled MSBuild task reads that manifest after `CoreCompile` and writes a fresh SwiftPM package under `obj/`.
4. `xcodebuild archive` builds that generated package directly from `Package.swift`, producing an xcframework and `Metadata.appintents`.
5. MSBuild adds the xcframework as a `NativeReference`, copies `Metadata.appintents` into the `.app`, and validates the final bundle before codesigning.

The generated Swift stays intentionally small: `AppIntent`, `AppEntity`, `EntityStringQuery`, `AppEnum`, and `AppShortcutsProvider` declarations plus a reusable JSON bridge.

## Setup

Reference the package and enable generation for iOS builds:

```xml
<ItemGroup>
  <PackageReference Include="Maui.AppIntents" Version="0.1.0-preview" />
</ItemGroup>

<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <MauiAppIntentsEnabled>true</MauiAppIntentsEnabled>
</PropertyGroup>
```

For local development in this repository the sample uses a `ProjectReference` and imports `buildTransitive/Maui.AppIntents.props` and `.targets` directly. NuGet consumers get those imports automatically.

Register your handlers, entity query handlers, and the package runtime:

```csharp
builder.Services.AddSingleton<ITaskService, TaskService>();
builder.Services.AddTransient<CreateTaskIntent>();
builder.Services.AddTransient<CompleteTaskIntent>();
builder.Services.AddTransient<TaskItemQueryHandler>();
builder.Services.AddMauiAppIntents();
```

Wire the generated native bridge after the MAUI service provider exists:

```csharp
#if MAUI_APPINTENTS
Maui.AppIntents.MauiAppIntentsNative.WireUp(IPlatformApplication.Current!.Services);
#endif
```

## Author an intent

An intent is a normal DI-created C# class that implements `IAppIntentHandler<TRequest>`.

```csharp
[AppIntent("CreateTaskIntent",
    Title = "Create Task",
    Description = "Creates a task from Shortcuts")]
[AppShortcut("Create a task in ${applicationName}",
    ShortTitle = "Create Task",
    SystemImageName = "plus.circle")]
public sealed class CreateTaskIntent : IAppIntentHandler<CreateTaskIntent.Request>
{
    private readonly ITaskService tasks;

    public CreateTaskIntent(ITaskService tasks)
    {
        this.tasks = tasks;
    }

    public sealed record Request(
        [property: IntentParameter("Title")] string Title,
        [property: IntentParameter("Estimated Minutes", IsOptional = true)] int? EstimatedMinutes,
        [property: IntentParameter("Priority", IsOptional = true)] TaskPriorityLevel? Priority);

    public Task<AppIntentResponse> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var task = tasks.Create(
            request.Title,
            request.Priority ?? TaskPriorityLevel.Medium,
            estimatedMinutes: request.EstimatedMinutes);

        return Task.FromResult(AppIntentResponse.Succeeded($"Created '{task.Title}'."));
    }
}
```

Supported parameter types in the current slice:

| C# type | Generated Swift type |
| --- | --- |
| `string` | `String` |
| `int`, `long` | `Int` |
| `float`, `double`, `decimal` | `Double` |
| `bool` | `Bool` |
| `DateTime`, `DateTimeOffset` | `Date` |
| nullable variants | optional Swift parameters |
| `[AppEnum]` enums | generated `AppEnum` |
| `AppEntityReference<TEntity>` | generated `AppEntity` |
| `IReadOnlyList<T>`, `List<T>`, `T[]` | multi-value Swift parameters |

Use `IAppIntentHandler<TRequest, TResult>` when an intent returns a value to Shortcuts. Supported result values are the same primitive, enum, entity reference, and collection shapes as parameters.

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
        var task = tasks.Create(request.Title);
        var reference = new AppEntityReference<TaskItem>
        {
            Id = task.Id,
            Display = task.Title,
            Subtitle = task.Notes
        };

        return Task.FromResult(
            AppIntentResponse<AppEntityReference<TaskItem>>.Succeeded(reference, $"Created '{task.Title}'."));
    }
}
```

## Add an enum

Annotate normal C# enums. The generated Swift uses an `Int`-backed `AppEnum` and passes raw values through the JSON bridge.

```csharp
[AppEnum("Task Priority")]
public enum TaskPriorityLevel
{
    [AppEnumCase("Low")]
    Low = 0,

    [AppEnumCase("Medium")]
    Medium = 1,

    [AppEnumCase("High")]
    High = 2
}
```

## Add an entity and dynamic query

Annotate the model you want users to pick in Shortcuts. The generator can use the conventions `Id`, `Title` or `Name`, and `Subtitle` or `Notes`, or you can mark properties explicitly.

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

    [AppEntityProperty("Completed")]
    public bool IsCompleted { get; set; }
}
```

Expose dynamic loading, search, and suggestions with an entity query handler:

```csharp
[AppEntityQueryHandler(typeof(TaskItem))]
public sealed class TaskItemQueryHandler : IAppEntityQueryHandler<TaskItem>
{
    private readonly ITaskService tasks;

    public TaskItemQueryHandler(ITaskService tasks)
    {
        this.tasks = tasks;
    }

    public Task<IReadOnlyList<TaskItem>> GetEntitiesAsync(
        IReadOnlyList<string> identifiers,
        CancellationToken cancellationToken)
    {
        var result = identifiers
            .Select(tasks.GetById)
            .Where(static task => task is not null)
            .Cast<TaskItem>()
            .ToList();

        return Task.FromResult<IReadOnlyList<TaskItem>>(result);
    }

    public Task<IReadOnlyList<TaskItem>> SearchEntitiesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(tasks.Search(query));
    }

    public Task<IReadOnlyList<TaskItem>> SuggestedEntitiesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(tasks.GetFiltered(showCompleted: false));
    }
}
```

Use `AppEntityReference<TEntity>` in request DTOs. It intentionally carries the selected entity reference (`Id`, `Display`, `Subtitle`) rather than a hydrated model, so handlers can re-fetch authoritative data from app services.

```csharp
[AppIntent("CompleteTaskIntent", Title = "Complete Task")]
[AppShortcut("Complete a task in ${applicationName}",
    ShortTitle = "Complete Task",
    SystemImageName = "checkmark.circle")]
public sealed class CompleteTaskIntent : IAppIntentHandler<CompleteTaskIntent.Request>
{
    private readonly ITaskService tasks;

    public CompleteTaskIntent(ITaskService tasks)
    {
        this.tasks = tasks;
    }

    public sealed record Request(
        [property: IntentParameter("Task")] AppEntityReference<TaskItem> Task);

    public Task<AppIntentResponse> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var task = tasks.GetById(request.Task.Id);
        if (task is null)
        {
            return Task.FromResult(AppIntentResponse.Failed("The selected task could not be found."));
        }

        tasks.Complete(task.Id);
        return Task.FromResult(AppIntentResponse.Succeeded($"Completed '{task.Title}'."));
    }
}
```

For multi-select entity parameters, use a collection of references:

```csharp
[AppIntent("CompleteTasksIntent", Title = "Complete Tasks")]
public sealed class CompleteTasksIntent : IAppIntentHandler<CompleteTasksIntent.Request, int>
{
    public sealed record Request(
        [property: IntentParameter("Tasks")] IReadOnlyList<AppEntityReference<TaskItem>> Tasks);

    public Task<AppIntentResponse<int>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var completed = request.Tasks.Count(task => tasks.Complete(task.Id));
        return Task.FromResult(AppIntentResponse<int>.Succeeded(completed, $"Completed {completed} tasks."));
    }
}
```

The build task generates a Swift `AppEntity` and `EntityStringQuery` for each referenced entity. Query operations use the same JSON bridge as intent execution:

| Operation | Swift query method | C# handler method |
| --- | --- | --- |
| `entities` | `entities(for:)` | `GetEntitiesAsync` |
| `matching` | `entities(matching:)` | `SearchEntitiesAsync` |
| `suggested` | `suggestedEntities()` | `SuggestedEntitiesAsync` |

Generated Swift query methods return empty results if the in-process dispatcher is not ready, which keeps Shortcuts parameter pickers from surfacing bridge errors during app startup or background query flows.

## Donate generated intents

The generator emits a stable native donation entry point. After `MauiAppIntentsNative.WireUp(...)` has loaded the generated framework, call `MauiAppIntentsNative.Donate(identifier, payload)` with C# property names matching the generated request shape.

```csharp
#if IOS && MAUI_APPINTENTS
Maui.AppIntents.MauiAppIntentsNative.Donate("CompleteTaskIntent", new
{
    task = new
    {
        id = task.Id,
        display = task.Title,
        subtitle = task.Notes
    }
});
#endif
```

Collections use JSON arrays of the same scalar/entity reference payloads.

## Advanced authoring

These declarative features emit additional static Swift that Apple's metadata extractor reads. They are validated against the Xcode toolchain in the sample build.

### Execution modes (foreground/background)

`SupportedModes` is the modern replacement for the boolean `OpenAppWhenRun`. The generator emits an iOS 26 `supportedModes` extension and keeps `openAppWhenRun` for pre-iOS-26 fallback.

```csharp
[AppIntent("CreateTaskIntent", Title = "Create Task",
    SupportedModes = AppIntentExecutionModes.Foreground)]
```

`Foreground` → `[.foreground]`, `Background` → `[.background]`, `ForegroundAndBackground` → `[.background, .foreground]`.

### Rich and conditional parameter summaries

Replace the single literal `Summary("…")` with one or more `[AppIntentSummary]` attributes. Use `{ParameterName}` tokens to interpolate parameters, and `WhenParameter`/`EqualsValue` for conditional summaries.

```csharp
[AppIntentSummary("Create {Title}")]
[AppIntentSummary("Create {Title} as an urgent task",
    WhenParameter = "Priority", EqualsValue = "Urgent")]
```

This generates a `When(\.$priority, .equalTo, TaskPriorityLevel.urgent) { Summary(...) } otherwise: { Summary(...) }` chain.

### Typed errors

Return categorized failures so iOS surfaces the right system error. The Swift runtime maps categories to `AppIntentError` cases (iOS 18+) and falls back to a generic error otherwise.

```csharp
return Task.FromResult(
    AppIntentResponse.Failed(AppIntentErrorCategory.EntityNotFound, "The task no longer exists."));
```

Categories include `NetworkFailure`, `NotAllowed`, `UnsupportedOnDevice`, `FeatureRestricted`, `EntityNotFound`, `NeedsSignIn`, `NeedsAccountSetup`, `NeedsConfirmation`, and `Permission*` variants.

### Spotlight indexing

Flag an entity as `Indexed` and tag properties with an indexing key to emit an `IndexedEntity` conformance that builds a `CSSearchableItemAttributeSet`.

```csharp
[AppEntity("TaskItem", Indexed = true)]
public sealed class TaskItem
{
    [AppEntityProperty("Details", IndexingKey = "contentDescription")]
    public string Details { get; set; } = "";
}
```

Allowed indexing keys: `contentDescription`, `title`, `keywords`.

### URL representations (deep links)

Provide a URL template on an entity or enum to emit `URLRepresentableEntity`/`URLRepresentableEnum`. Use `{id}` for the entity identifier and `{PropertyName}` for `@Property` values.

```csharp
[AppEntity("TaskItem", UrlRepresentation = "tasktracker://task/{id}?details={Details}")]
public sealed class TaskItem { /* … */ }

[AppEnum("Task Priority", UrlRepresentation = "tasktracker://priority")]
public enum TaskPriorityLevel { /* … */ }
```

### Singleton (unique) entities

For settings-style singletons, set `Unique = true`. The generator emits `UniqueAppEntity` + `UniqueAppEntityQuery` instead of a string query, and the registry's `"unique"` operation returns the first suggested entity.

```csharp
[AppEntity("TaskTrackerSettings", Unique = true)]
public sealed class TaskTrackerSettings
{
    [AppEntityIdentifier]
    public string Id { get; set; } = "settings";

    [AppEntityDisplay]
    public string Title { get; set; } = "Task Tracker Settings";
}
```

> **iOS 18 cascade:** `UniqueAppEntity` forces the entity and any intent that references it to iOS 18+. Because `@AppShortcutsBuilder` cannot use `if #available`, such intents are gated with `@available(iOS 18.0, *)` and are **excluded from the generated App Shortcuts provider** (they remain available in the Shortcuts editor and via donation). All other advanced features above are emitted as gated extensions that keep base types at the iOS 17 minimum.

### Confirmation before running

Set `RequiresConfirmation = true` to make the generated `perform()` ask the system to confirm the action (via `requestConfirmation`) before invoking your C# handler — the common "confirm before doing X" flow. The prompt is gated to iOS 18+; on earlier versions the action proceeds without an explicit prompt.

```csharp
[AppIntent("CompleteGeneratedTaskIntent",
    Title = "Complete Generated Task",
    RequiresConfirmation = true,
    ConfirmationDialog = "Mark this task as complete?",
    ConfirmationActionName = AppIntentConfirmationAction.Set)]
public sealed class CompleteGeneratedTaskIntent : IAppIntentHandler<CompleteGeneratedTaskIntent.Request>
{
    // ...
}
```

`ConfirmationDialog` defaults to the intent title when omitted. `ConfirmationActionName` maps to Apple's `ConfirmationActionName` verbs (`Continue`, `Set`, `Buy`, `Send`, `Delete`-style actions, etc.) used to label the confirmation button.

### File input parameters

Declare a parameter of type `AppIntentFile` to receive a file from another app, the share sheet, or the Files app. The generator maps it to Apple's `IntentFile` parameter, and the file bytes are bridged to C# as a base64 payload, exposed as `byte[] Data` along with `FileName` and `ContentType`.

```csharp
[AppIntent("ImportTaskFileIntent", Title = "Import Task From File")]
public sealed class ImportTaskFileIntent : IAppIntentHandler<ImportTaskFileIntent.Request, AppEntityReference<TaskItem>>
{
    public sealed record Request(
        [property: IntentParameter("File")] AppIntentFile File);

    public Task<AppIntentResponse<AppEntityReference<TaskItem>>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var bytes = request.File.Data;       // raw file content
        var name = request.File.FileName;    // e.g. "notes.txt"
        // ...
    }
}
```

`List<AppIntentFile>` is also supported for multi-file inputs. File parameters are excluded from generated donations (a file payload cannot be reconstructed for a donated intent).

### Dynamic options for string parameters

Entity query handlers supply options for entity-backed parameters. For plain **string** parameters, implement `IAppIntentOptionsProvider` and mark it with `[AppIntentOptionsProvider("<identifier>")]`, then link it to a parameter with `OptionsProvider`. The generator emits a Swift `DynamicOptionsProvider`, and the option list is produced by your C# code at suggestion time (so it can reflect live app state).

```csharp
[AppIntentOptionsProvider("taskTags")]
public sealed class TaskTagOptionsProvider : IAppIntentOptionsProvider
{
    private readonly ITaskService tasks;

    public TaskTagOptionsProvider(ITaskService tasks) => this.tasks = tasks;

    public Task<IReadOnlyList<AppIntentOption>> GetOptionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<AppIntentOption> options = new[] { "Work", "Home", "Errand" }
            .Select(t => new AppIntentOption(t))   // optional second arg sets a display label
            .ToList();
        return Task.FromResult(options);
    }
}

// On the intent's Request:
[property: IntentParameter("Tag", OptionsProvider = "taskTags")] string Tag
```

Register the provider in DI (`builder.Services.AddTransient<TaskTagOptionsProvider>();`). Options providers apply to **non-optional, non-collection string** parameters only; the identifier must match a `[AppIntentOptionsProvider]` type or the generator reports `MAUIAI007`.

## Build

Simulator:

```bash
dotnet build MyApp.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=
```

Device:

```bash
dotnet build MyApp.csproj -f net10.0-ios
```

Useful properties:

| Property | Values | Purpose |
| --- | --- | --- |
| `MauiAppIntentsEnabled` | `true` / `false` | Enables the generated App Intents pipeline |
| `MauiAppIntentsModuleName` | Swift identifier | Overrides the generated SwiftPM module/framework name |
| `MauiAppIntentsArchivePlatforms` | `Simulator`, `Device`, `Both` | Controls which native archives are built |
| `MauiAppIntentsMinimumOSVersion` | e.g. `17.0` | Sets SwiftPM iOS platform and deployment target |
| `MauiAppIntentsValidateBundle` | `true` / `false` | Enables final app bundle validation |

`MauiAppIntentsArchivePlatforms` defaults to `Simulator` for simulator RIDs, `Device` for device RIDs, and `Both` for RID-less iOS builds.

## Validate outputs

After a successful build, the `.app` should contain:

```text
Frameworks/<MauiAppIntentsModuleName>.framework/
Metadata.appintents/extract.actionsdata
Metadata.appintents/version.json
```

The validation target checks that:

- `Metadata.appintents` is inside the app bundle.
- The generated embedded framework is present.
- The framework exports the stable `MauiAppIntentBridgeSetDispatcher` and `MauiAppIntentBridgeDonate` C ABI symbols.
- `extract.actionsdata` contains generated intent, shortcut, enum, entity, and entity property strings from the manifest.

The sample has been validated on an iPhone 17 simulator. It generates `CreateGeneratedTaskIntent`, `CompleteGeneratedTaskIntent`, `CompleteGeneratedTasksIntent`, `ListGeneratedTasksIntent`, `TaskPriorityLevel`, `TaskCategoryType`, `TaskItemEntity`, and `TaskItemEntityQuery`; Shortcuts execution reaches C# through the generated JSON dispatcher.

## Current scope

Implemented:

- C# intent handlers and request DTOs.
- Primitive, optional, enum, entity, and multi-select parameters.
- Typed result values through `IAppIntentHandler<TRequest, TResult>` and generated Swift `ReturnsValue<T>`.
- Generated `AppShortcut` phrases.
- Generated `AppEnum`, `AppEntity`, and `EntityStringQuery` declarations.
- Generated entity `@Property` fields from `[AppEntityProperty]`.
- Dynamic entity lookup, search, and suggested entities through DI query handlers.
- Modern `supportedModes`/`IntentModes` execution modes (with `openAppWhenRun` fallback).
- Rich and conditional `ParameterSummary` expressions via `[AppIntentSummary]`.
- Typed `AppIntentError` categories via `AppIntentResponse.Failed(AppIntentErrorCategory, …)`.
- `IndexedEntity` + indexing keys for Spotlight; `URLRepresentableEntity`/`URLRepresentableEnum` deep links.
- `UniqueAppEntity`/`UniqueAppEntityQuery` singleton entities.
- Pre-execution confirmation via `[AppIntent(RequiresConfirmation = true, …)]` → gated `requestConfirmation`.
- `IntentFile` file-input parameters via the `AppIntentFile` parameter type (single or `List<>`).
- Dynamic options for string parameters via `IAppIntentOptionsProvider` → generated `DynamicOptionsProvider`.
- Generated native donation entry point via `MauiAppIntentsNative.Donate`.
- Source-generator diagnostics for malformed authoring patterns.
- Generated C# registration and native bridge glue.
- Generated SwiftPM package, `xcodebuild archive`, xcframework embedding, metadata copy, and bundle validation.

Not yet implemented (gaps vs Apple's App Intents framework, 2024–2026):

Declarative gaps (achievable by generating more static Swift from C# metadata):

- `SyncableEntity` cross-device IDs (conformance not present in the installed iOS SDK).
- Apple Intelligence assistant schemas (`app-schema-domains` via `@AssistantIntent`/`@AssistantEntity`/`@AssistantEnum`) — generatable but the largest declarative workstream.

Bridge/runtime gaps (need new reusable-shim + JSON-dispatch contracts):

- Interaction flow: `requestConfirmation` pre-execution confirmation is **implemented** (`RequiresConfirmation`); conditional `requestConfirmation(conditions:)`, `requestValue`, and disambiguation are not yet.
- Real cancellation (`CancellableIntent`/`IntentCancellationReason`) wired into the handler `CancellationToken`.
- `LongRunningIntent`/progress, `UndoableIntent`. (`IntentFile` file-input parameters are **implemented** via `AppIntentFile`; `FileEntity` is not yet.)
- `Transferable`/`NSUserActivity` onscreen awareness, `EntityPropertyQuery`, `EntityCollection`, `AppUnionValue`/`@UnionValue`, `OwnershipProvidingEntity`, `RelevantEntities`. (`DynamicOptionsProvider` for string parameters is **implemented** via `IAppIntentOptionsProvider`.)

Architectural / UI gaps (separate effort, may need hand-authored Swift):

- Out-of-process App Intents extension and `allowedExecutionTargets`/`IntentExecutionTargets`.
- Interactive snippets / result snippet views (`SnippetIntent`, `ShowsSnippetView`) — SwiftUI, not expressible from C#.
- `ControlConfigurationIntent` (see the Widgets sample), `CameraCaptureIntent`, `AudioRecordingIntent`, `IntentValueQuery` (visual intelligence).
- `PredictableIntent`/prediction configuration generation.

See `plan.md` ("Audit: C#-first App Intents vs Apple documentation (2024–2026)") for the full feature matrix and prioritized roadmap.
