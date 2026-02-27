# C# Binding & Bridge Patterns

Complete code patterns for the .NET iOS binding library and C# bridge implementation.

## Table of Contents
1. [Binding Project Setup](#binding-project-setup)
2. [ApiDefinition.cs](#apidefinitioncs)
3. [C# Bridge Implementation](#c-bridge-implementation)
4. [AppDelegate Wiring](#appdelegate-wiring)
5. [MAUI Project Configuration](#maui-project-configuration)

---

## Binding Project Setup

Create with: `dotnet new iosbinding -n {AppName}.Binding`

### Binding .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-ios</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>true</ImplicitUsings>
    <IsBindingProject>true</IsBindingProject>
  </PropertyGroup>

  <ItemGroup>
    <ObjcBindingApiDefinition Include="ApiDefinition.cs" />
    <ObjcBindingCoreSource Include="StructsAndEnums.cs" />
  </ItemGroup>

  <!-- Build the Xcode project automatically via Native Library Interop -->
  <ItemGroup>
    <XcodeProject Include="../{AppName}.AppIntents/{FrameworkName}.xcodeproj">
      <SchemeName>{FrameworkName}</SchemeName>
      <ForceLoad>true</ForceLoad>
      <SmartLink>false</SmartLink>
    </XcodeProject>
  </ItemGroup>

  <!-- Extract App Intents Metadata from the xcarchive after the Xcode project builds -->
  <Target Name="ExtractAppIntentsMetadata" AfterTargets="_BuildXcodeProjects">
    <PropertyGroup>
      <_MetadataOutputDir>$(IntermediateOutputPath)xcode/Metadata.appintents</_MetadataOutputDir>
      <_XcodeOutputDir>$(IntermediateOutputPath)xcode</_XcodeOutputDir>
    </PropertyGroup>
    <Exec Command="rm -rf &quot;$(_MetadataOutputDir)&quot;" Condition="Exists('$(_MetadataOutputDir)')" />
    <Exec Command="METADATA_SRC=%24(find &quot;$(_XcodeOutputDir)&quot; -path &apos;*/archives/*Metadata.appintents&apos; -type d | head -1) &amp;&amp; if [ -n &quot;%24METADATA_SRC&quot; ]; then cp -R &quot;%24METADATA_SRC&quot; &quot;$(_MetadataOutputDir)&quot; &amp;&amp; echo &quot;Copied Metadata.appintents&quot;; else echo &quot;WARNING: Metadata.appintents not found&quot;; fi" />
  </Target>
</Project>
```

**How `<XcodeProject>` works:**
- The .NET iOS SDK's `_BuildXcodeProjects` target automatically: calls `xcodebuild archive` (device + simulator), creates an xcframework, and adds it as a `NativeReference` — no shell scripts needed.
- `ForceLoad=true` ensures the framework's ObjC classes are loaded even if not directly referenced — without this, the App Intents metadata types won't be registered with the runtime.
- `SmartLink=false` prevents the linker from stripping "unused" symbols that App Intents actually needs.

**The `ExtractAppIntentsMetadata` target:**
- Runs after `_BuildXcodeProjects` to find `Metadata.appintents` in the xcarchive output and copy it to a known location (`obj/.../xcode/Metadata.appintents`).
- Uses `%24()` to escape shell `$()` from MSBuild property expansion.
- The MAUI project's `CopyAppIntentsMetadata` target then copies this into the app bundle.

---

## ApiDefinition.cs

This file maps every @objc type from the Swift bridge layer to C#. There are three kinds of types:

### 1. DTO Class (maps the bridge data transfer object)

```csharp
using Foundation;
using ObjCRuntime;

namespace {AppName}.Binding
{
    [BaseType(typeof(NSObject))]
    interface Bridge{EntityName}
    {
        [Export("id")]
        string Id { get; set; }

        [Export("name")]
        string Name { get; set; }

        [Export("description_")]
        string Description { get; set; }

        [Export("typeRawValue")]
        nint TypeRawValue { get; set; }

        [Export("statusRawValue")]
        nint StatusRawValue { get; set; }

        [NullAllowed, Export("date")]
        NSDate Date { get; set; }

        [Export("count")]
        nint Count { get; set; }

        [Export("isActive")]
        bool IsActive { get; set; }

        [Export("createdAt")]
        NSDate CreatedAt { get; set; }

        [Export("initWithId:name:description_:typeRawValue:statusRawValue:date:count:isActive:createdAt:")]
        NativeHandle Constructor(string id, string name, string description,
            nint typeRawValue, nint statusRawValue,
            [NullAllowed] NSDate date, nint count,
            bool isActive, NSDate createdAt);
    }
```

**Rules:**
- Use `nint` for `Int` properties (not `int`) — ObjC integers are pointer-sized.
- Use `NSDate` for dates (not `DateTime`) — conversion happens in C# bridge code.
- Use `[NullAllowed]` attribute for nullable reference types. Do NOT use C# `?` syntax in ApiDefinition.cs.
- The `[Export("...")]` selector string must match the ObjC property/method name exactly.
- Constructor `[Export]` follows ObjC naming: `initWith{FirstParam}:{secondParam}:{thirdParam}:` — each `:` marks a parameter.

### 2. Protocol (the bridge protocol C# will implement)

```csharp
    [Protocol, Model]
    [BaseType(typeof(NSObject))]
    interface {DataProviderName}
    {
        [Abstract, Export("getAll")]
        Bridge{EntityName}[] GetAll();

        [Abstract, Export("getWithId:")]
        [return: NullAllowed]
        Bridge{EntityName} Get(string id);

        [Abstract, Export("createWithName:typeRawValue:statusRawValue:date:count:notes:")]
        [return: NullAllowed]
        Bridge{EntityName} Create(string name, nint typeRawValue, nint statusRawValue,
            [NullAllowed] NSDate date, nint count, string notes);

        [Abstract, Export("deleteWithId:")]
        bool Delete(string id);

        [Abstract, Export("searchWithQuery:")]
        Bridge{EntityName}[] Search(string query);

        [Abstract, Export("getFilteredWithTypeRawValue:statusRawValue:showCompleted:")]
        Bridge{EntityName}[] GetFiltered(nint typeRawValue, nint statusRawValue, bool showCompleted);
    }
```

**Rules:**
- `[Protocol, Model]` together tell the binding generator this is an ObjC protocol with a default Model class.
- `[Abstract]` marks required protocol methods.
- The generator produces both `I{DataProviderName}` (C# interface) and `{DataProviderName}` (abstract class to inherit from).
- Use `[return: NullAllowed]` for methods that can return nil.
- Array return types use `Bridge{EntityName}[]` — the binding generator handles NSArray conversion.

### 3. Manager Singleton

```csharp
    [BaseType(typeof(NSObject))]
    [DisableDefaultCtor]
    interface {BridgeManagerName}
    {
        [Static, Export("shared")]
        {BridgeManagerName} Shared { get; }

        [NullAllowed, Export("provider", ArgumentSemantic.Weak)]
        {DataProviderName} Provider { get; set; }
    }
}
```

**Rules:**
- `[DisableDefaultCtor]` prevents C# from creating new instances — this is a singleton.
- `[Static]` on `Shared` maps to the Swift `static let shared`.
- `ArgumentSemantic.Weak` mirrors the Swift `weak var` — important to prevent retain cycles.
- The `Provider` property type uses the concrete `{DataProviderName}` (Model class), not `I{DataProviderName}` (interface). The C# bridge inherits from the Model class.

### ObjC Selector Names

Getting the selector strings right is the trickiest part. Here's how Swift method signatures map to ObjC selectors:

| Swift | ObjC Selector |
|-------|--------------|
| `func getAllTasks() -> [T]` | `getAllTasks` |
| `func getTask(withId id: String) -> T?` | `getTaskWithId:` |
| `func createTask(title: String, priority: Int) -> T?` | `createTaskWithTitle:priority:` |
| `func setDueDate(_ date: Date, forTaskWithId id: String)` | `setDueDate:forTaskWithId:` |

The pattern: first argument label becomes part of the method name, subsequent labels become parameter names separated by colons.

### 4. Intent Donation Bridge

```csharp
    [BaseType(typeof(NSObject))]
    [DisableDefaultCtor]
    interface IntentDonationBridge
    {
        [Static, Export("shared")]
        IntentDonationBridge Shared { get; }

        [Export("donateCreateTaskWithTitle:priorityRawValue:categoryRawValue:dueDate:estimatedMinutes:notes:")]
        void DonateCreateTask(string title, nint priorityRawValue, nint categoryRawValue,
            [NullAllowed] NSDate dueDate, nint estimatedMinutes, string notes);

        [Export("donateCompleteTaskWithTaskId:taskTitle:")]
        void DonateCompleteTask(string taskId, string taskTitle);

        [Export("donateOpenTaskWithTaskId:taskTitle:")]
        void DonateOpenTask(string taskId, string taskTitle);

        [Export("donateSetDueDate:taskId:taskTitle:")]
        void DonateSetDueDate(NSDate date, string taskId, string taskTitle);

        [Export("donateSearchTasksWithQuery:")]
        void DonateSearchTasks(string query);

        [Export("deleteTaskDonationsWithTaskId:")]
        void DeleteTaskDonations(string taskId);
    }
```

**Rules:**
- Same singleton pattern as the BridgeManager.
- Methods are `void` return — donation is fire-and-forget (the Swift side handles errors with try/catch + logging).
- Provide one donate method per intent type, plus deletion methods.

---

## C# Bridge Implementation

This class lives in `Platforms/iOS/` and implements the Swift protocol by inheriting from the generated Model class.

```csharp
using Foundation;
using {AppName}.Binding;
using {AppName}.Models;
using {AppName}.Services;

namespace {AppName}.Platforms.iOS;

public class AppIntentsBridgeProvider : {DataProviderName}
{
    private readonly I{ServiceName} _service;

    public AppIntentsBridgeProvider(I{ServiceName} service)
    {
        _service = service;
    }

    public override Bridge{EntityName}[] GetAll()
    {
        return _service.GetAll().Select(ToBridge).ToArray();
    }

    public override Bridge{EntityName}? Get(string id)
    {
        var item = _service.GetById(id);
        return item is not null ? ToBridge(item) : null;
    }

    public override Bridge{EntityName}? Create(string name, nint typeRawValue,
        nint statusRawValue, NSDate? date, nint count, string notes)
    {
        var created = _service.Create(
            name,
            ({TypeEnum})(int)typeRawValue,
            ({StatusEnum})(int)statusRawValue,
            date is not null ? (DateTime)date : null,
            (int)count >= 0 ? (int)count : null,
            string.IsNullOrEmpty(notes) ? null : notes
        );
        return ToBridge(created);
    }

    public override bool Delete(string id)
    {
        return _service.Delete(id);
    }

    public override Bridge{EntityName}[] Search(string query)
    {
        return _service.Search(query).Select(ToBridge).ToArray();
    }

    public override Bridge{EntityName}[] GetFiltered(nint typeRawValue,
        nint statusRawValue, bool showCompleted)
    {
        {TypeEnum}? type = (int)typeRawValue >= 0
            ? ({TypeEnum})(int)typeRawValue : null;
        {StatusEnum}? status = (int)statusRawValue >= 0
            ? ({StatusEnum})(int)statusRawValue : null;

        return _service.GetFiltered(type, status, showCompleted)
            .Select(ToBridge).ToArray();
    }

    /// Converts a C# model to a Swift-compatible bridge DTO.
    private static Bridge{EntityName} ToBridge({EntityModel} item)
    {
        return new Bridge{EntityName}(
            id: item.Id,
            name: item.Name,
            description: item.Description ?? "",
            typeRawValue: (nint)(int)item.Type,
            statusRawValue: (nint)(int)item.Status,
            date: item.Date.HasValue ? (NSDate)item.Date.Value : null,
            count: item.Count.HasValue ? (nint)item.Count.Value : -1,
            isActive: item.IsActive,
            createdAt: (NSDate)item.CreatedAt
        );
    }
}
```

**Key conversion patterns:**
- `nint` ↔ `int`: Cast through `(int)nintValue` or `(nint)intValue`
- C# enum ↔ `nint`: `(MyEnum)(int)rawValue` and `(nint)(int)enumValue`
- `DateTime` ↔ `NSDate`: `(DateTime)nsDate` and `(NSDate)dateTime` (implicit conversions exist)
- Nullable DateTime: `date.HasValue ? (NSDate)date.Value : null`
- Sentinel to nullable: `(int)value >= 0 ? (int)value : null` for ints; `string.IsNullOrEmpty(s) ? null : s` for strings

---

## AppDelegate Wiring

Wire the bridge in `AppDelegate.FinishedLaunching`, after the MAUI app is initialized:

```csharp
using Foundation;
using UIKit;
using {AppName}.Binding;
using {AppName}.Platforms.iOS;
using {AppName}.Services;

namespace {AppName};

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        // Wire up the Swift App Intents bridge
        try
        {
            var service = IPlatformApplication.Current?.Services.GetService<I{ServiceName}>();
            if (service is not null)
            {
                var bridge = new AppIntentsBridgeProvider(service);
                {BridgeManagerName}.Shared.Provider = bridge;
                Console.WriteLine("[AppIntents] Bridge wired up successfully.");
            }
            else
            {
                Console.WriteLine("[AppIntents] WARNING: Service not found in DI.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppIntents] ERROR wiring bridge: {ex.Message}");
        }

        return result;
    }
}
```

**Important:**
- `base.FinishedLaunching(...)` must be called FIRST — it initializes MAUI and the DI container.
- Use `IPlatformApplication.Current?.Services` to access DI — the standard `MauiApplication.Current` may not be ready yet in all code paths.
- Wrap in try/catch — a crash here prevents the entire app from launching.
- The bridge must be set up here (not later) because iOS may invoke intents during app launch.

---

## MAUI Project Configuration

### .csproj additions for App Intents

Add these ItemGroups to the MAUI `.csproj`:

```xml
<!-- Reference the binding project -->
<ItemGroup>
    <ProjectReference Include="..\{AppName}.Binding\{AppName}.Binding.csproj" />
</ItemGroup>

<!-- Copy App Intents Metadata from the binding project's intermediate output into the app bundle -->
<Target Name="CopyAppIntentsMetadata" AfterTargets="_CopyResourcesToBundle"
        Condition="$(TargetFramework.Contains('ios'))">
    <PropertyGroup>
        <_MetadataSource>../MauiAppIntentsSample.Binding/obj/$(Configuration)/$(TargetFramework)/xcode/Metadata.appintents</_MetadataSource>
    </PropertyGroup>
    <ItemGroup>
        <_AppIntentsMetadata Include="$(_MetadataSource)/**/*" Condition="Exists('$(_MetadataSource)')" />
    </ItemGroup>
    <Copy SourceFiles="@(_AppIntentsMetadata)"
          DestinationFolder="$(AppBundleDir)/Metadata.appintents/%(RecursiveDir)"
          Condition="'@(_AppIntentsMetadata)' != ''" />
    <Message Text="Copied App Intents Metadata to $(AppBundleDir)/Metadata.appintents" Importance="high"
             Condition="'@(_AppIntentsMetadata)' != ''" />
    <Warning Text="Metadata.appintents not found at $(_MetadataSource) — App Intents won't work at runtime"
             Condition="!Exists('$(_MetadataSource)')" />
</Target>
```

**The `CopyAppIntentsMetadata` target is critical.** Without it, the compiled `Metadata.appintents` directory won't be in the app bundle, and iOS will never discover the intents. The `AfterTargets="_CopyResourcesToBundle"` hook runs at the right point in the MAUI build pipeline.

**The metadata path** points to the binding project's intermediate output (`obj/.../xcode/Metadata.appintents`) where the `ExtractAppIntentsMetadata` target placed it during the binding build.

**Important path detail:** Always use `/` separators in `DestinationFolder`, not `\`. The `$(AppBundleDir)` property may not end with a trailing path separator — using `$(AppBundleDir)/Metadata.appintents` is safe, but `$(AppBundleDir)Metadata.appintents` or `$(AppBundleDir)\Metadata.appintents` will concatenate incorrectly, placing metadata outside the bundle.

### Entitlements.plist

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.developer.siri</key>
    <true/>
    <key>com.apple.security.application-groups</key>
    <array>
        <string>group.com.{company}.{appname}</string>
    </array>
</dict>
</plist>
```

The Siri entitlement is required. The App Group is optional but useful if you later add a widget or Siri extension that needs shared data.

---

## Intent Donation Service

### C# Interface

```csharp
public interface IIntentDonationService
{
    void DonateCreateTask(TaskItem task);
    void DonateCompleteTask(TaskItem task);
    void DonateOpenTask(TaskItem task);
    void DonateSetDueDate(TaskItem task);
    void DonateSearchTasks(string query);
    void DeleteTaskDonations(string taskId);
}
```

### iOS Implementation

Lives in `Platforms/iOS/` so it only compiles for iOS:

```csharp
public class IntentDonationService : IIntentDonationService
{
    public void DonateCreateTask(TaskItem task)
    {
        try
        {
            IntentDonationBridge.Shared.DonateCreateTask(
                task.Title,
                (nint)task.Priority,
                (nint)task.Category,
                task.DueDate.HasValue ? (NSDate)task.DueDate.Value : null,
                task.EstimatedMinutes ?? -1,
                task.Notes ?? string.Empty
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppIntents] Donation failed: {ex.Message}");
        }
    }
    
    // ... other methods follow the same pattern
}
```

### DI Registration

```csharp
// In MauiProgram.cs
var builder = MauiApp.CreateBuilder();
#if IOS
builder.Services.AddSingleton<IIntentDonationService, IntentDonationService>();
#endif
```

### ViewModel Integration

Inject the donation service as an optional dependency to keep ViewModels cross-platform:

```csharp
public partial class TaskDetailViewModel : ObservableObject
{
    private readonly ITaskService _taskService;
    private readonly IIntentDonationService? _donationService;
    
    public TaskDetailViewModel(ITaskService taskService,
        IIntentDonationService? donationService = null)
    {
        _taskService = taskService;
        _donationService = donationService;
    }
    
    [RelayCommand]
    private async Task Save()
    {
        var task = _taskService.Create(Title, Priority, Category, DueDate, EstimatedMinutes, Notes);
        _donationService?.DonateCreateTask(task);
        await Shell.Current.GoToAsync("..");
    }
}
```

**Key patterns:**
- `IIntentDonationService? donationService = null` — nullable with default null so ViewModels work on all platforms
- Always wrap bridge calls in try/catch — donation failures must never affect the app
- Donate AFTER the action succeeds, not before — you're telling the system what the user did, not what they're about to do
