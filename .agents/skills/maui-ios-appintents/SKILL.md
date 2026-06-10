---
name: maui-ios-appintents
description: >
  Add Apple Siri App Intents to .NET MAUI iOS apps. Prefer the generated C#-first
  Maui.AppIntents package path: C# attributes, Roslyn semantic discovery, generated
  SwiftPM declarations, generated xcframework, and Metadata.appintents bundle validation.
  Use this skill whenever someone wants Siri, Shortcuts, App Intents, AppEntity,
  AppEnum, App Shortcuts, Spotlight, Apple Intelligence actions, or voice commands
  in a .NET MAUI or Xamarin iOS app. Also use it for the legacy manual Swift
  framework + binding-library bridge when the generated package does not yet cover
  the requested App Intents feature.
---

# .NET MAUI + iOS App Intents

Use the generated `Maui.AppIntents` path first. It lets app developers author intents, enums, entities, and query handlers in C# while the build generates the Swift declarations Apple needs for App Intents metadata extraction.

The older manual Swift framework + .NET iOS binding approach remains useful as a fallback for advanced features that are not generated yet, but it should not be the default recommendation in this repository.

## Architecture to prefer

```
.NET MAUI app
  C# [AppIntent] handlers
  C# [AppEnum] enums
  C# [AppEntity] models
  C# IAppEntityQueryHandler<TEntity>
        |
        v
Roslyn incremental generator
  emits managed registration and native bridge glue
  embeds JSON manifest in assembly metadata
        |
        v
Compiled MSBuild task after CoreCompile
  reads manifest from intermediate assembly
  writes SwiftPM package under obj/
  emits AppIntent/AppEnum/AppEntity/EntityStringQuery/AppShortcutsProvider
        |
        v
xcodebuild archive from Package.swift
  produces xcframework + Metadata.appintents
        |
        v
MAUI build
  adds NativeReference before _ExpandNativeReferences
  copies Metadata.appintents into .app before codesigning
  validates metadata, framework, bridge symbols, phrases, enums, entities, properties
```

This still uses Swift and Xcode internally because iOS discovers intents from compile-time Swift metadata. The win is that the app developer does not maintain Swift code, a generated `.xcodeproj`, or binding definitions for the generated path.

## Generated path workflow

### 1. Enable the package

In the MAUI app project:

```xml
<ItemGroup>
  <PackageReference Include="Maui.AppIntents" Version="0.1.0-preview" />
</ItemGroup>

<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <MauiAppIntentsEnabled>true</MauiAppIntentsEnabled>
</PropertyGroup>
```

For local repository work, use the project-reference pattern already in the sample and import `Maui.AppIntents/buildTransitive/Maui.AppIntents.props` and `.targets`.

### 2. Create C# intent handlers

Each generated intent is a DI-created class implementing `IAppIntentHandler<TRequest>`. Use `IAppIntentHandler<TRequest, TResult>` when the intent returns a value to Shortcuts.

```csharp
[AppIntent("CreateTaskIntent",
    Title = "Create Task",
    Description = "Creates a new task")]
[AppShortcut("Create a task in ${applicationName}",
    ShortTitle = "Create Task",
    SystemImageName = "plus.circle")]
public sealed class CreateTaskIntent
    : IAppIntentHandler<CreateTaskIntent.Request, AppEntityReference<TaskItem>>
{
    private readonly ITaskService tasks;

    public CreateTaskIntent(ITaskService tasks)
    {
        this.tasks = tasks;
    }

    public sealed record Request(
        [property: IntentParameter("Title")] string Title,
        [property: IntentParameter("Priority", IsOptional = true)] TaskPriorityLevel? Priority);

    public Task<AppIntentResponse<AppEntityReference<TaskItem>>> HandleAsync(
        Request request,
        CancellationToken cancellationToken)
    {
        var task = tasks.Create(request.Title, request.Priority ?? TaskPriorityLevel.Medium);
        return Task.FromResult(AppIntentResponse<AppEntityReference<TaskItem>>.Succeeded(
            new AppEntityReference<TaskItem>
            {
                Id = task.Id,
                Display = task.Title,
                Subtitle = task.Notes
            },
            $"Created '{task.Title}'."));
    }
}
```

Supported generated parameter and result types today: `string`, integer numeric types, floating numeric types, `bool`, `DateTime`, `DateTimeOffset`, nullable variants, `[AppEnum]`, `AppEntityReference<TEntity>`, and collection shapes such as `IReadOnlyList<T>`, `List<T>`, and `T[]`.

### 3. Add AppEnum types

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

The generated Swift enum is `Int` backed and passes raw values through JSON so `System.Text.Json` can hydrate C# enum properties.

### 4. Add AppEntity models and dynamic queries

Annotate the C# model. Use explicit attributes or the conventions `Id`, `Title`/`Name`, and `Subtitle`/`Notes`.

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
```

Add a DI query handler. This powers Shortcuts parameter pickers, lookup by stored IDs, Siri disambiguation, and suggested entities.

```csharp
[AppEntityQueryHandler(typeof(TaskItem))]
public sealed class TaskItemQueryHandler : IAppEntityQueryHandler<TaskItem>
{
    private readonly ITaskService tasks;

    public TaskItemQueryHandler(ITaskService tasks)
    {
        this.tasks = tasks;
    }

    public Task<IReadOnlyList<TaskItem>> GetEntitiesAsync(IReadOnlyList<string> ids, CancellationToken token)
        => Task.FromResult<IReadOnlyList<TaskItem>>(ids.Select(tasks.GetById).OfType<TaskItem>().ToList());

    public Task<IReadOnlyList<TaskItem>> SearchEntitiesAsync(string query, CancellationToken token)
        => Task.FromResult(tasks.Search(query));

    public Task<IReadOnlyList<TaskItem>> SuggestedEntitiesAsync(CancellationToken token)
        => Task.FromResult(tasks.GetFiltered(showCompleted: false));
}
```

Use the entity from intents through `AppEntityReference<TEntity>`:

```csharp
[AppIntent("CompleteTaskIntent", Title = "Complete Task")]
[AppShortcut("Complete a task in ${applicationName}", ShortTitle = "Complete Task")]
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

Do not deserialize selected entities as full app models. The generated Swift sends an entity reference payload (`id`, `display`, `subtitle`); the handler should re-fetch by ID from app services.

For multi-select entity parameters, use collection references:

```csharp
[AppIntent("CompleteTasksIntent", Title = "Complete Tasks")]
[AppShortcut("Complete tasks in ${applicationName}", ShortTitle = "Complete Tasks")]
public sealed class CompleteTasksIntent : IAppIntentHandler<CompleteTasksIntent.Request, int>
{
    private readonly ITaskService tasks;

    public CompleteTasksIntent(ITaskService tasks)
    {
        this.tasks = tasks;
    }

    public sealed record Request(
        [property: IntentParameter("Tasks")] IReadOnlyList<AppEntityReference<TaskItem>> Tasks);

    public Task<AppIntentResponse<int>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var completed = request.Tasks.Count(task => tasks.Complete(task.Id));
        return Task.FromResult(AppIntentResponse<int>.Succeeded(completed, $"Completed {completed} tasks."));
    }
}
```

### 5. Register services and wire native bridge

```csharp
builder.Services.AddSingleton<ITaskService, TaskService>();
builder.Services.AddTransient<CreateTaskIntent>();
builder.Services.AddTransient<CompleteTaskIntent>();
builder.Services.AddTransient<TaskItemQueryHandler>();
builder.Services.AddMauiAppIntents();
```

In iOS `AppDelegate.FinishedLaunching`, after `base.FinishedLaunching`:

```csharp
#if MAUI_APPINTENTS
Maui.AppIntents.MauiAppIntentsNative.WireUp(IPlatformApplication.Current!.Services);
#endif
```

Donate generated intents from iOS code after the bridge is wired:

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

### 6. Build and inspect outputs

Simulator:

```bash
dotnet build MyApp.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=
```

Device:

```bash
dotnet build MyApp.csproj -f net10.0-ios
```

Confirm the `.app` contains:

```text
Frameworks/<MauiAppIntentsModuleName>.framework/
Metadata.appintents/extract.actionsdata
Metadata.appintents/version.json
```

The validation target should fail the build if generated metadata, shortcut phrases, enum/entity/property names, the embedded framework, or the generated dispatcher/donation bridge symbols are missing.

## Generated-path gotchas

1. **Swift is still required internally.** Do not promise a no-Xcode toolchain story. The generated path removes user-authored Swift/Xcode assets, not Apple's Swift metadata extraction requirement.
2. **Entity query handlers must be fast.** Query methods may be called by Shortcuts UI and Siri disambiguation. Use lightweight app-service calls and avoid long UI-thread work.
3. **Entity query bridge failures return empty results.** Generated Swift catches bridge-not-ready and query errors for `EntityStringQuery` methods so parameter pickers degrade instead of throwing user-visible errors.
4. **`AppEntityReference<TEntity>` is a reference, not a model.** Re-fetch by `Id` in handlers.
5. **Generated diagnostics are intentional guardrails.** Fix `MAUIAI001`-`MAUIAI006` authoring diagnostics instead of suppressing them; they identify missing request types, unsupported parameter/result shapes, invalid identifiers, and handler/query mismatches.
6. **Module names must be stable Swift identifiers.** Set `MauiAppIntentsModuleName` if the default project-derived name is not acceptable.
7. **Metadata must be inside the app bundle.** If intents do not appear, inspect `{App}.app/Metadata.appintents/`.
8. **Siri voice still needs entitlements/provisioning.** Shortcuts app execution can work while Siri voice fails if the app or provisioning profile lacks Siri capability.

## When to use the manual Swift/binding fallback

Use the legacy Swift framework + binding library pattern when the generated package does not yet support the feature the user needs, such as:

- advanced result protocols not covered by generated `ReturnsValue<T>` value results
- complex `ParameterSummary` or `PredictableIntent` configurations
- app extensions or out-of-process execution models

For manual fallback details, read:

| File | Contents |
| --- | --- |
| `references/swift-patterns.md` | Swift `AppIntent`, `AppEntity`, `EntityQuery`, `AppEnum`, shortcut, donation, and bridge patterns |
| `references/csharp-binding.md` | Binding library and C# bridge implementation patterns |
| `references/build-integration.md` | `<XcodeProject>` build integration and metadata copy patterns |

## Completion checklist for generated path

- [ ] `MauiAppIntentsEnabled=true` is set for iOS builds.
- [ ] Intent handlers are registered in DI before `AddMauiAppIntents()`.
- [ ] Entity query handlers are registered in DI.
- [ ] `MauiAppIntentsNative.WireUp(...)` runs in iOS startup.
- [ ] Simulator build succeeds with generated SwiftPM archive.
- [ ] Generated `.app` contains the framework and `Metadata.appintents`.
- [ ] `extract.actionsdata` contains expected intent identifiers, shortcut phrases, enum names, entity names, and query names.
- [ ] Shortcuts app shows generated shortcuts under the app.
- [ ] Tapping a generated shortcut reaches the C# handler.
- [ ] Entity picker/search returns dynamic app data from `IAppEntityQueryHandler<TEntity>`.
- [ ] Device build uses a provisioning profile with Siri capability before testing Siri voice.
