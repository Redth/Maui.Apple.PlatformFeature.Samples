# Maui.AppIntents

Prototype package for C#-authored App Intents in .NET MAUI iOS apps.

The intended flow is:

1. Author intent handlers in C# with attributes.
2. MSBuild scans the attributed C# during iOS builds when `MauiAppIntentsEnabled=true`.
3. MSBuild emits app-specific Swift declarations, a reusable Swift JSON bridge, and C# native interop glue under `obj/`.
4. `xcodebuild` archives a fresh SwiftPM package directly from `Package.swift`; no checked-in `.xcodeproj` is needed.
5. The generated xcframework is added as a `NativeReference`, `Metadata.appintents` is copied into the app bundle, and the final bundle is validated before codesigning.

The reusable part is the C# API, generated dispatcher, native bridge glue, and Swift runtime JSON bridge. The per-app generated Swift stays intentionally small: only the `AppIntent`, `AppEntity`, `AppEnum`, `EntityQuery`, and `AppShortcutsProvider` declarations Apple needs to extract metadata.

At runtime the generated C# glue loads the embedded SwiftPM framework from `NSBundle.MainBundle.PrivateFrameworksPath` and resolves a stable C ABI entry point with `dlopen`/`dlsym`. This avoids depending on direct `[DllImport("<FrameworkName>")]` resolution, which is not reliable for copied embedded frameworks on iOS.

## C# authoring example

```csharp
[AppIntent("CreateTaskIntent",
    Title = "Create Task",
    Description = "Creates a new task")]
[AppShortcut("Create a task in ${applicationName}",
    ShortTitle = "Create Task",
    SystemImageName = "plus.circle")]
public sealed class CreateTaskIntent :
    IAppIntentHandler<CreateTaskIntent.Request>
{
    public sealed class Request
    {
        [IntentParameter("Title")]
        public string TaskTitle { get; set; } = "";

        [IntentParameter("Estimated Minutes", IsOptional = true)]
        public int? EstimatedMinutes { get; set; }
    }

    public Task<AppIntentResponse> HandleAsync(
        Request request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(AppIntentResponse.Succeeded(
            $"Created {request.TaskTitle}"));
    }
}
```

Request positional records are supported too:

```csharp
public sealed record Request(
    [property: IntentParameter("Title")] string TaskTitle,
    [property: IntentParameter("Estimated Minutes", IsOptional = true)] int? EstimatedMinutes);
```

Enable the build integration in the app project:

```xml
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <MauiAppIntentsEnabled>true</MauiAppIntentsEnabled>
</PropertyGroup>
```

Register services:

```csharp
builder.Services.AddTransient<CreateTaskIntent>();
builder.Services.AddMauiAppIntents();
```

Wire the generated native bridge after the MAUI service provider exists:

```csharp
#if MAUI_APPINTENTS
Maui.AppIntents.MauiAppIntentsNative.WireUp(IPlatformApplication.Current!.Services);
#endif
```

`MauiAppIntentsArchivePlatforms` can be set to `Simulator`, `Device`, or `Both`. By default, simulator RIDs build only simulator archives, device RIDs build only device archives, and RID-less builds archive both. `MauiAppIntentsValidateBundle` defaults to `true`; set it to `false` only when diagnosing the build pipeline.

## Build validation

The package injects into the iOS build graph through MSBuild dependency properties instead of fragile `BeforeTargets`/`AfterTargets` hooks. The generated SwiftPM archive runs only when its generated Swift, runtime shim, package manifest, or build script inputs change. After the SDK has assembled the `.app` bundle and before codesigning, the validation gate checks:

- `Metadata.appintents/extract.actionsdata` and `version.json` are present in the app bundle.
- The generated embedded framework is present in `Frameworks/`.
- The framework exports the stable `MauiAppIntentBridgeSetDispatcher` C ABI symbol.
- `extract.actionsdata` contains the generated intent identifiers and shortcut phrases from the normalized generator manifest.

## Simulator validation

The current vertical slice has been validated with the sample app on an iPhone 17 simulator:

- `dotnet build -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=` builds the MAUI app, generated SwiftPM package, xcframework, and `Metadata.appintents`.
- The app bundle contains `Frameworks/<ModuleName>.framework` and `Metadata.appintents/extract.actionsdata`.
- The generated framework exports `MauiAppIntentBridgeSetDispatcher`.
- On launch, logs include `[AppIntents] Generated bridge wired up successfully.`
- The simulator indexes the generated shortcut phrase in the app's custom vocabulary, e.g. `Create a generated task in TaskTracker`.
- Tapping the generated App Shortcut tile in Shortcuts, entering `Test`, invokes the generated SwiftPM `AppIntent` and reaches the C# handler. Simulator logs include `[AppIntents] Generated handler invoked: Test`, and the MAUI app shows the created `Test` task.
- `shortcuts://run-shortcut?...` and `/usr/bin/shortcuts run ...` target user-authored Shortcuts by name; in this simulator they do not directly invoke App Shortcuts generated from `AppShortcutsProvider`.

## Current v1 scope

- Primitive parameters: `string`, `int`, `long`, `double`, `float`, `decimal`, `bool`, `DateTime`, and nullable variants.
- Generated `AppIntent` declarations with JSON dispatch into C# handlers.
- Generated `AppShortcutsProvider` from `[AppShortcut]`.
- Generated C# registration and native bridge glue.
- Generated SwiftPM package + `xcodebuild archive` + xcframework + `Metadata.appintents`.
- Automated bundle validation for generated metadata, shortcut phrases, embedded framework, and bridge symbol.

Entities, enums, rich result values, and `ParameterSummary` generation are the next layer; the build/bridge path is intentionally structured so those declarations can be added without reintroducing app-authored Swift or a checked-in Xcode project.
