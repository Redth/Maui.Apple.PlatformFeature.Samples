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
- Generated native donation entry point via `MauiAppIntentsNative.Donate`.
- Source-generator diagnostics for malformed authoring patterns.
- Generated C# registration and native bridge glue.
- Generated SwiftPM package, `xcodebuild archive`, xcframework embedding, metadata copy, and bundle validation.

Not yet implemented (gaps vs Apple's App Intents framework, 2024–2026):

Declarative gaps (achievable by generating more static Swift from C# metadata):

- Modern `supportedModes`/`IntentModes` (the current idiom replacing the `OpenAppWhenRun` boolean).
- `IndexedEntity` + `@Property(indexingKey:)` Spotlight indexing.
- `URLRepresentableIntent`/`URLRepresentableEntity`/`URLRepresentableEnum` deep links.
- `UniqueAppEntity`/`UniqueAppEntityQuery` singleton entities and `SyncableEntity` cross-device IDs.
- Rich `ParameterSummary` expressions (only literal `Summary("…")` is generated today).
- Typed `AppIntentError` categories.
- Apple Intelligence assistant schemas (`app-schema-domains` via `@AssistantIntent`/`@AssistantEntity`/`@AssistantEnum`) — generatable but the largest declarative workstream.

Bridge/runtime gaps (need new reusable-shim + JSON-dispatch contracts):

- Interaction flow: `requestConfirmation` (incl. conditional), `requestValue`, disambiguation.
- Real cancellation (`CancellableIntent`/`IntentCancellationReason`) wired into the handler `CancellationToken`.
- `LongRunningIntent`/progress, `UndoableIntent`, `IntentFile`/`FileEntity` parameters.
- `Transferable`/`NSUserActivity` onscreen awareness, `EntityPropertyQuery`, `DynamicOptionsProvider`, `EntityCollection`, `AppUnionValue`/`@UnionValue`, `OwnershipProvidingEntity`, `RelevantEntities`.

Architectural / UI gaps (separate effort, may need hand-authored Swift):

- Out-of-process App Intents extension and `allowedExecutionTargets`/`IntentExecutionTargets`.
- Interactive snippets / result snippet views (`SnippetIntent`, `ShowsSnippetView`) — SwiftUI, not expressible from C#.
- `ControlConfigurationIntent` (see the Widgets sample), `CameraCaptureIntent`, `AudioRecordingIntent`, `IntentValueQuery` (visual intelligence).
- `PredictableIntent`/prediction configuration generation.

See `plan.md` ("Audit: C#-first App Intents vs Apple documentation (2024–2026)") for the full feature matrix and prioritized roadmap.
